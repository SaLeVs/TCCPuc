#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Missions.Editor
{
    // Um cano para o cálculo: os vértices do modelo no espaço do cano (o pivô fica em cima do spawn).
    public sealed class PipeFitPiece
    {
        public string Name;
        public Vector3 RotationAxis = Vector3.right;
        public List<Vector3> Vertices = new();
    }

    public sealed class PipeFitCell
    {
        public int Column;
        public int Row;
        public PipeFitPiece Piece;
        public List<int> CorrectSteps = new();

        // Onde a célula fica na grade de referência (SpawnList), local ao SpawnList.
        public Vector3 Reference;
    }

    // Cano que já está no PipesManager e não se mexe (ex.: a entrada com a válvula).
    // Vértices locais ao SpawnList.
    public sealed class PipeFitFixed
    {
        public string Name;
        public List<Vector3> Vertices = new();
    }

    // Uma célula ligada numa peça fixa: o cano dela tem que nascer exatamente em Target.
    public sealed class PipeFitAnchor
    {
        public PipeFitCell Cell;
        public string FixedName;
        public Vector3 Target;
        public float ErrorBefore;
        public float ErrorAfter;
    }

    // Uma ligação entre dois canos vizinhos: onde o To tem que ficar em relação ao From
    // para a boca de um encostar na boca do outro, centralizadas.
    public sealed class PipeFitJoint
    {
        public PipeFitCell From;
        public PipeFitCell To;
        public Vector3 Required;
        public float ErrorBefore;
        public float ErrorAfter;
        public float WorstOtherCorrectSteps;
    }

    public sealed class PipeFitResult
    {
        // Célula -> onde o cano dela nasce, local ao SpawnList.
        public readonly Dictionary<PipeFitCell, Vector3> Positions = new();
        public readonly List<PipeFitJoint> Joints = new();
        public readonly List<PipeFitAnchor> Anchors = new();
        public readonly List<string> Problems = new();

        // Células com passos corretos errados ou faltando -> os passos que deveriam ser
        // (todos os que deixam o cano igual ao primeiro passo da lista).
        public readonly Dictionary<PipeFitCell, List<int>> SuggestedSteps = new();
        public readonly List<string> Notes = new();

        public float MaxErrorBefore;
        public float MaxErrorAfter;
        public string Report;

        public bool CanApply => Problems.Count == 0 && Positions.Count > 0;
    }

    // O cálculo do encaixe de um grid, sem nada de editor: mede as bocas dos modelos e acha a posição
    // de cada célula para as bocas vizinhas se encostarem, sem mexer em scale. Separado para dar para testar.
    public static class PipeSpawnFitSolver
    {
        // Abaixo disso conta como encaixado (0,5 mm).
        public const float Tolerance = 0.0005f;

        // Boca de cano = o anel na ponta do modelo. Lateral de cano dá uma faixa comprida, não um anel.
        private const float MinMouthRoundness = 0.6f;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private static readonly Dictionary<(PipeFitPiece, float), Vector3[]> RotatedCache = new();


        // columnDirection / rowDirection: para onde crescem as colunas e as linhas, local ao SpawnList.
        // fixedPieces: canos parados no PipesManager. Uma boca solta de célula que fica de frente para uma
        // boca deles (a menos de captureDistance) liga nela, e o grid é posicionado para essa ligação fechar.
        public static PipeFitResult Solve(string layoutName, int columns, int rows, IReadOnlyList<PipeFitCell> cells,
            IReadOnlyList<float> angles, Vector3 columnDirection, Vector3 rowDirection,
            IReadOnlyList<PipeFitFixed> fixedPieces = null, float captureDistance = 0f)
        {
            RotatedCache.Clear();
            PipeFitResult result = new();

            columnDirection = SnapToAxis(columnDirection);
            rowDirection = SnapToAxis(rowDirection);

            Dictionary<Vector2Int, PipeFitCell> valid = BuildJoints(columns, rows, cells, angles, columnDirection, rowDirection, result);

            if (fixedPieces != null && fixedPieces.Count > 0)
            {
                BuildAnchors(valid, angles, columnDirection, rowDirection, fixedPieces, captureDistance, result);
            }

            foreach (PipeFitJoint joint in result.Joints)
            {
                joint.ErrorBefore = JointError(joint, joint.From.Reference, joint.To.Reference);
                result.MaxErrorBefore = Mathf.Max(result.MaxErrorBefore, joint.ErrorBefore);
            }

            foreach (PipeFitAnchor anchor in result.Anchors)
            {
                anchor.ErrorBefore = (anchor.Cell.Reference - anchor.Target).magnitude;
                result.MaxErrorBefore = Mathf.Max(result.MaxErrorBefore, anchor.ErrorBefore);
            }

            if (result.Problems.Count == 0)
            {
                SolvePositions(cells, result);

                foreach (PipeFitJoint joint in result.Joints)
                {
                    joint.ErrorAfter = JointError(joint, result.Positions[joint.From], result.Positions[joint.To]);
                    result.MaxErrorAfter = Mathf.Max(result.MaxErrorAfter, joint.ErrorAfter);
                }

                foreach (PipeFitAnchor anchor in result.Anchors)
                {
                    anchor.ErrorAfter = (result.Positions[anchor.Cell] - anchor.Target).magnitude;
                    result.MaxErrorAfter = Mathf.Max(result.MaxErrorAfter, anchor.ErrorAfter);
                }

                PipeFitJoint worstJoint = result.Joints.OrderByDescending(j => j.ErrorAfter).FirstOrDefault();
                PipeFitAnchor worstAnchor = result.Anchors.OrderByDescending(a => a.ErrorAfter).FirstOrDefault();

                if (worstAnchor != null && worstAnchor.ErrorAfter > Tolerance)
                {
                    result.Problems.Add($"As peças fixas ({string.Join(", ", result.Anchors.Select(a => a.FixedName).Distinct())}) " +
                                        $"pedem posições diferentes para o caminho: sobra {Mm(worstAnchor.ErrorAfter)} em " +
                                        $"{Label(worstAnchor.Cell)}. Com esses canos o caminho não fecha entre elas.");
                }
                else if (worstJoint != null && worstJoint.ErrorAfter > Tolerance)
                {
                    result.Problems.Add($"O caminho fecha um ciclo que não bate: sobra {Mm(worstJoint.ErrorAfter)} entre " +
                                        $"{Label(worstJoint.From)} e {Label(worstJoint.To)}.");
                }
            }

            result.Report = BuildReport(layoutName, result);
            return result;
        }

        // Quanto falta para as bocas se tocarem com os canos nessas posições.
        public static float JointError(PipeFitJoint joint, Vector3 fromPosition, Vector3 toPosition)
        {
            return (toPosition - fromPosition - joint.Required).magnitude;
        }

        // Centro da boca do cano (girado em angle) na ponta que aponta para direction,
        // relativo ao pivô. Falso se essa ponta não é uma boca (é a lateral do cano).
        public static bool TryFindMouth(PipeFitPiece piece, float angle, Vector3 direction, out Vector3 mouth)
        {
            mouth = Vector3.zero;

            // Ângulo que não leva os lados da grade em lados da grade (ex.: 45°) deixa o cano na diagonal:
            // nenhuma boca fica de frente para um vizinho.
            Vector3 turned = Quaternion.AngleAxis(angle, piece.RotationAxis) * direction;
            if (Mathf.Max(Mathf.Abs(turned.x), Mathf.Abs(turned.y), Mathf.Abs(turned.z)) < 0.999f) return false;

            Vector3[] vertices = Rotated(piece, angle);

            if (vertices.Length == 0) return false;

            Vector3 u = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 w = Vector3.Cross(direction, u);

            float face = float.MinValue;
            float back = float.MaxValue;

            foreach (Vector3 vertex in vertices)
            {
                float along = Vector3.Dot(vertex, direction);
                face = Mathf.Max(face, along);
                back = Mathf.Min(back, along);
            }

            float tolerance = Mathf.Max(0.0005f, (face - back) * 0.01f);

            float uMin = float.MaxValue, uMax = float.MinValue;
            float wMin = float.MaxValue, wMax = float.MinValue;

            foreach (Vector3 vertex in vertices)
            {
                if (Vector3.Dot(vertex, direction) < face - tolerance) continue;

                float onU = Vector3.Dot(vertex, u);
                float onW = Vector3.Dot(vertex, w);

                uMin = Mathf.Min(uMin, onU);
                uMax = Mathf.Max(uMax, onU);
                wMin = Mathf.Min(wMin, onW);
                wMax = Mathf.Max(wMax, onW);
            }

            float extentU = uMax - uMin;
            float extentW = wMax - wMin;
            float largest = Mathf.Max(extentU, extentW);

            mouth = direction * face + u * ((uMin + uMax) * 0.5f) + w * ((wMin + wMax) * 0.5f);

            return largest > 0f && Mathf.Min(extentU, extentW) / largest >= MinMouthRoundness;
        }

        public static string Label(PipeFitCell cell) => $"{cell.Column},{cell.Row}";

        public static string Mm(float meters) => $"{(meters * 1000f).ToString("0.0", Invariant)} mm";


        private static Dictionary<Vector2Int, PipeFitCell> BuildJoints(int columns, int rows, IReadOnlyList<PipeFitCell> cells,
            IReadOnlyList<float> angles, Vector3 columnDirection, Vector3 rowDirection, PipeFitResult result)
        {
            Dictionary<Vector2Int, PipeFitCell> byPosition = new();

            foreach (PipeFitCell cell in cells)
            {
                if (cell.Piece == null)
                {
                    result.Problems.Add($"Célula {Label(cell)}: o prefab não tem PipeTotem com modelo.");
                    continue;
                }

                if (cell.CorrectSteps == null || cell.CorrectSteps.Count == 0 ||
                    cell.CorrectSteps.Any(step => step < 0 || step >= angles.Count))
                {
                    result.Problems.Add($"Célula {Label(cell)}: passos corretos vazios ou fora da lista de ângulos (0 a {angles.Count - 1}).");
                    continue;
                }

                if (!CheckSteps(cell, angles, columnDirection, rowDirection, result)) continue;

                byPosition[new Vector2Int(cell.Column, cell.Row)] = cell;
            }

            foreach (PipeFitCell cell in byPosition.Values)
            {
                float angle = angles[cell.CorrectSteps[0]];

                TryLink(cell, byPosition, new Vector2Int(1, 0), columnDirection, angles, result);
                TryLink(cell, byPosition, new Vector2Int(0, 1), rowDirection, angles, result);

                // Boca virada para uma célula vazia do grid: água vazando. Para fora do grid pode (é a parede).
                CheckOpenSide(columns, rows, cell, byPosition, new Vector2Int(1, 0), columnDirection, angle, result);
                CheckOpenSide(columns, rows, cell, byPosition, new Vector2Int(-1, 0), -columnDirection, angle, result);
                CheckOpenSide(columns, rows, cell, byPosition, new Vector2Int(0, 1), rowDirection, angle, result);
                CheckOpenSide(columns, rows, cell, byPosition, new Vector2Int(0, -1), -rowDirection, angle, result);
            }

            return byPosition;
        }

        // Liga as bocas soltas das células nas bocas das peças fixas que estão de frente para elas.
        private static void BuildAnchors(Dictionary<Vector2Int, PipeFitCell> cells, IReadOnlyList<float> angles,
            Vector3 columnDirection, Vector3 rowDirection, IReadOnlyList<PipeFitFixed> fixedPieces, float captureDistance,
            PipeFitResult result)
        {
            (Vector3 direction, Vector2Int offset)[] sides =
            {
                (columnDirection, new Vector2Int(1, 0)), (-columnDirection, new Vector2Int(-1, 0)),
                (rowDirection, new Vector2Int(0, 1)), (-rowDirection, new Vector2Int(0, -1))
            };

            List<(PipeFitCell cell, Vector3 direction, Vector3 mouth)> open = new();

            foreach (PipeFitCell cell in cells.Values)
            {
                foreach ((Vector3 direction, Vector2Int offset) in sides)
                {
                    cells.TryGetValue(new Vector2Int(cell.Column, cell.Row) + offset, out PipeFitCell neighbour);

                    bool linked = neighbour != null && result.Joints.Any(j =>
                        (j.From == cell && j.To == neighbour) || (j.From == neighbour && j.To == cell));

                    if (linked) continue;

                    if (TryFindMouth(cell.Piece, angles[cell.CorrectSteps[0]], direction, out Vector3 mouth))
                    {
                        open.Add((cell, direction, mouth));
                    }
                }
            }

            foreach (PipeFitFixed piece in fixedPieces)
            {
                PipeFitPiece shape = new() { Name = piece.Name, Vertices = piece.Vertices };

                foreach ((Vector3 direction, _) in sides)
                {
                    if (!TryFindMouth(shape, 0f, direction, out Vector3 fixedMouth)) continue;

                    var candidates = open
                        .Where(o => o.direction == -direction && result.Anchors.All(a => a.Cell != o.cell))
                        .Select(o => (o.cell, o.mouth, distance: (o.cell.Reference + o.mouth - fixedMouth).magnitude))
                        .Where(o => o.distance <= captureDistance)
                        .OrderBy(o => o.distance)
                        .ToList();

                    if (candidates.Count == 0) continue;

                    result.Anchors.Add(new PipeFitAnchor
                    {
                        Cell = candidates[0].cell,
                        FixedName = piece.Name,
                        Target = fixedMouth - candidates[0].mouth
                    });
                }
            }
        }

        // O primeiro passo da lista é o que vale (é o que a prévia mostra). Os outros passos corretos têm
        // que deixar o cano abrindo para os mesmos lados (o reto em 0° e 180°), e nenhum desses pode faltar:
        // senão o jogo aceita um cano virado errado, ou recusa um que está visualmente certo.
        private static bool CheckSteps(PipeFitCell cell, IReadOnlyList<float> angles, Vector3 columnDirection,
            Vector3 rowDirection, PipeFitResult result)
        {
            int first = cell.CorrectSteps[0];
            int expected = OpenSides(cell.Piece, angles[first], columnDirection, rowDirection);

            if (expected == 0)
            {
                result.Problems.Add($"Célula {Label(cell)}: no passo {first} ({Deg(angles[first])}) o cano não abre para nenhum " +
                                    "lado da grade (fica na diagonal). Escolha outro primeiro passo.");
                return false;
            }

            // O primeiro passo continua na frente: é a rotação que a prévia e o encaixe usam.
            List<int> equivalent = Enumerable.Range(0, angles.Count)
                .Where(step => OpenSides(cell.Piece, angles[step], columnDirection, rowDirection) == expected)
                .OrderBy(step => step == first ? -1 : step)
                .ToList();

            List<int> wrong = cell.CorrectSteps.Distinct().Where(step => !equivalent.Contains(step)).ToList();
            List<int> missing = equivalent.Where(step => !cell.CorrectSteps.Contains(step)).ToList();

            foreach (int step in wrong)
            {
                int sides = OpenSides(cell.Piece, angles[step], columnDirection, rowDirection);
                result.Problems.Add($"Célula {Label(cell)}: o passo {step} ({Deg(angles[step])}) conta como certo, mas nele o cano " +
                                    $"abre para {SideNames(sides)}, e no passo {first} abre para {SideNames(expected)}.");
            }

            foreach (int step in missing)
            {
                result.Problems.Add($"Célula {Label(cell)}: falta o passo {step} ({Deg(angles[step])}). Nele o cano fica igual " +
                                    $"ao passo {first} ({SideNames(expected)}), mas o jogo não contaria como certo.");
            }

            if (wrong.Count > 0 || missing.Count > 0)
            {
                result.SuggestedSteps[cell] = equivalent;
            }

            return true;
        }

        // Bits: 1 = direita (próxima coluna), 2 = esquerda, 4 = baixo (próxima linha), 8 = cima.
        private static int OpenSides(PipeFitPiece piece, float angle, Vector3 columnDirection, Vector3 rowDirection)
        {
            int sides = 0;

            if (TryFindMouth(piece, angle, columnDirection, out _)) sides |= 1;
            if (TryFindMouth(piece, angle, -columnDirection, out _)) sides |= 2;
            if (TryFindMouth(piece, angle, rowDirection, out _)) sides |= 4;
            if (TryFindMouth(piece, angle, -rowDirection, out _)) sides |= 8;

            return sides;
        }

        public static string SideNames(int sides)
        {
            List<string> names = new();

            if ((sides & 8) != 0) names.Add("cima");
            if ((sides & 4) != 0) names.Add("baixo");
            if ((sides & 2) != 0) names.Add("esquerda");
            if ((sides & 1) != 0) names.Add("direita");

            return names.Count > 0 ? string.Join(" e ", names) : "nenhum lado (diagonal)";
        }

        // Para o inspector: para que lados o cano abre nesse ângulo.
        public static string OpenSidesText(PipeFitPiece piece, float angle, Vector3 columnDirection, Vector3 rowDirection)
        {
            return SideNames(OpenSides(piece, angle, SnapToAxis(columnDirection), SnapToAxis(rowDirection)));
        }

        private static string Deg(float angle) => $"{angle.ToString("0.#", Invariant)}°";

        private static void TryLink(PipeFitCell from, Dictionary<Vector2Int, PipeFitCell> byPosition, Vector2Int offset,
            Vector3 direction, IReadOnlyList<float> angles, PipeFitResult result)
        {
            if (!byPosition.TryGetValue(new Vector2Int(from.Column, from.Row) + offset, out PipeFitCell to)) return;

            bool fromOpens = TryFindMouth(from.Piece, angles[from.CorrectSteps[0]], direction, out Vector3 fromMouth);
            bool toOpens = TryFindMouth(to.Piece, angles[to.CorrectSteps[0]], -direction, out Vector3 toMouth);

            // Vizinhos que não se ligam (dois caminhos lado a lado) não puxam um ao outro.
            if (!fromOpens && !toOpens) return;

            if (fromOpens != toOpens)
            {
                PipeFitCell open = fromOpens ? from : to;
                PipeFitCell closed = fromOpens ? to : from;
                result.Problems.Add($"{Label(open)} abre para {Label(closed)}, mas {Label(closed)} não abre de volta. " +
                                    "Confira o cano e o passo correto dessas duas células.");
                return;
            }

            PipeFitJoint joint = new() { From = from, To = to, Required = fromMouth - toMouth };

            // O jogador pode parar em qualquer passo correto (ex.: o reto em 0° ou 180°).
            foreach (int fromStep in from.CorrectSteps)
            {
                foreach (int toStep in to.CorrectSteps)
                {
                    bool a = TryFindMouth(from.Piece, angles[fromStep], direction, out Vector3 otherFrom);
                    bool b = TryFindMouth(to.Piece, angles[toStep], -direction, out Vector3 otherTo);

                    // Passo que não liga já foi acusado no CheckSteps.
                    if (!a || !b) continue;

                    float deviation = (otherFrom - otherTo - joint.Required).magnitude;
                    joint.WorstOtherCorrectSteps = Mathf.Max(joint.WorstOtherCorrectSteps, deviation);
                }
            }

            result.Joints.Add(joint);
        }

        private static void CheckOpenSide(int columns, int rows, PipeFitCell cell, Dictionary<Vector2Int, PipeFitCell> byPosition,
            Vector2Int offset, Vector3 direction, float angle, PipeFitResult result)
        {
            Vector2Int target = new Vector2Int(cell.Column, cell.Row) + offset;

            bool insideGrid = target.x >= 0 && target.x < columns && target.y >= 0 && target.y < rows;
            if (!insideGrid || byPosition.ContainsKey(target)) return;

            if (TryFindMouth(cell.Piece, angle, direction, out _))
            {
                result.Notes.Add($"{Label(cell)} tem uma boca virada para a célula vazia {target.x},{target.y}.");
            }
        }

        // Cada célula vai para onde as ligações mandam. Cada grupo de canos ligados fica centrado
        // onde estava na grade de referência, então o puzzle não sai do lugar na parede.
        private static void SolvePositions(IReadOnlyList<PipeFitCell> cells, PipeFitResult result)
        {
            Dictionary<PipeFitCell, List<(PipeFitCell other, Vector3 delta)>> links = new();

            foreach (PipeFitCell cell in cells.Where(c => c.Piece != null))
            {
                links[cell] = new List<(PipeFitCell, Vector3)>();
            }

            foreach (PipeFitJoint joint in result.Joints)
            {
                links[joint.From].Add((joint.To, joint.Required));
                links[joint.To].Add((joint.From, -joint.Required));
            }

            HashSet<PipeFitCell> visited = new();

            foreach (PipeFitCell start in links.Keys.OrderBy(c => c.Row).ThenBy(c => c.Column))
            {
                if (!visited.Add(start)) continue;

                List<PipeFitCell> group = new() { start };
                Dictionary<PipeFitCell, Vector3> solved = new() { [start] = start.Reference };
                Queue<PipeFitCell> queue = new();
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    PipeFitCell current = queue.Dequeue();

                    foreach ((PipeFitCell other, Vector3 delta) in links[current])
                    {
                        if (!visited.Add(other)) continue;

                        solved[other] = solved[current] + delta;
                        group.Add(other);
                        queue.Enqueue(other);
                    }
                }

                // Num ciclo as ligações podem pedir coisas diferentes: espalha o erro (mínimos quadrados).
                for (int iteration = 0; iteration < 500; iteration++)
                {
                    foreach (PipeFitCell cell in group)
                    {
                        if (links[cell].Count == 0) continue;

                        Vector3 sum = Vector3.zero;

                        foreach ((PipeFitCell other, Vector3 delta) in links[cell])
                        {
                            sum += solved[other] - delta;
                        }

                        solved[cell] = sum / links[cell].Count;
                    }
                }

                // Ligado numa peça fixa: o grupo vai para onde a ligação manda. Senão fica centrado onde estava.
                List<PipeFitAnchor> anchors = result.Anchors.Where(a => group.Contains(a.Cell)).ToList();

                Vector3 shift = anchors.Count > 0
                    ? Average(anchors.Select(a => a.Target - solved[a.Cell]))
                    : Average(group.Select(c => c.Reference)) - Average(group.Select(c => solved[c]));

                foreach (PipeFitCell cell in group)
                {
                    result.Positions[cell] = solved[cell] + shift;
                }
            }
        }

        private static string BuildReport(string layoutName, PipeFitResult result)
        {
            StringBuilder report = new();

            report.AppendLine($"Encaixe do grid {layoutName}: {result.Joints.Count} ligações entre canos.");

            if (result.Joints.Count > 0)
            {
                PipeFitJoint worst = result.Joints.OrderByDescending(j => j.ErrorBefore).First();
                report.AppendLine($"Na grade do SpawnList, a pior ficaria desencaixada em {Mm(worst.ErrorBefore)} " +
                                  $"({Label(worst.From)} → {Label(worst.To)}).");
            }

            foreach (PipeFitAnchor anchor in result.Anchors)
            {
                report.AppendLine($"Liga na peça fixa {anchor.FixedName} pela célula {Label(anchor.Cell)} " +
                                  $"(na grade do SpawnList ficaria {Mm(anchor.ErrorBefore)} fora).");
            }

            if (result.Anchors.Count == 0 && result.Problems.Count == 0)
            {
                report.AppendLine("Nenhuma peça fixa do PipesManager encosta neste caminho: ele fica centrado onde estava na grade.");
            }

            if (result.Problems.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Não dá para encaixar ainda:");
                foreach (string problem in result.Problems) report.AppendLine($"  • {problem}");

                if (result.SuggestedSteps.Count > 0)
                {
                    report.AppendLine();
                    report.AppendLine("Correção dos passos (pelo primeiro passo de cada célula):");

                    foreach ((PipeFitCell cell, List<int> steps) in result.SuggestedSteps.OrderBy(p => p.Key.Row).ThenBy(p => p.Key.Column))
                    {
                        report.AppendLine($"  • {Label(cell)}: [{string.Join(", ", cell.CorrectSteps)}] → [{string.Join(", ", steps)}]");
                    }
                }
            }
            else if (result.Positions.Count > 0)
            {
                report.AppendLine($"Com as posições deste grid: maior folga {Mm(result.MaxErrorAfter)}.");
            }

            float worstOther = result.Joints.Count > 0 ? result.Joints.Max(j => j.WorstOtherCorrectSteps) : 0f;

            if (worstOther > Tolerance)
            {
                report.AppendLine($"Obs.: num outro passo correto (ex.: o reto girado 180°) uma ligação desloca até {Mm(worstOther)}, " +
                                  "porque o modelo não é simétrico em volta do pivô.");
            }

            if (result.Notes.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Avisos:");
                foreach (string note in result.Notes.Distinct()) report.AppendLine($"  • {note}");
            }

            return report.ToString().TrimEnd();
        }

        private static Vector3[] Rotated(PipeFitPiece piece, float angle)
        {
            if (RotatedCache.TryGetValue((piece, angle), out Vector3[] cached)) return cached;

            Quaternion rotation = Quaternion.AngleAxis(angle, piece.RotationAxis);
            Vector3[] rotated = piece.Vertices.Select(v => rotation * v).ToArray();

            RotatedCache[(piece, angle)] = rotated;
            return rotated;
        }

        private static Vector3 SnapToAxis(Vector3 vector)
        {
            Vector3 abs = new(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
            int axis = abs.x >= abs.y && abs.x >= abs.z ? 0 : abs.y >= abs.z ? 1 : 2;

            Vector3 snapped = Vector3.zero;
            snapped[axis] = Mathf.Sign(vector[axis]);
            return snapped;
        }

        private static Vector3 Average(IEnumerable<Vector3> vectors)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (Vector3 vector in vectors)
            {
                sum += vector;
                count++;
            }

            return count > 0 ? sum / count : Vector3.zero;
        }
    }
}
#endif
