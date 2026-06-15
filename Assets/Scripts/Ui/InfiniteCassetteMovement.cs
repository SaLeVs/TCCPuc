using UnityEngine;

public class InfiniteCassetteMovement : MonoBehaviour
{
    [Tooltip("Movement speed to the right")]
    public float speed = 150f;

    [Tooltip("Drag the second image (Film_Top_Fundo) here")]
    public RectTransform secondImage;

    [Tooltip("Ajuste de encaixe das bordas. Mude esse valor se as bolinhas forem cortadas!")]
    public float overlapOffset = 135.06f; // Já deixei o seu valor salvo aqui!

    private RectTransform firstImage;
    private float imageWidth;
    private float rightBoundary;

    void Start()
    {
        firstImage = GetComponent<RectTransform>();

        // Aplica o desconto do offset na largura útil
        imageWidth = firstImage.rect.width - overlapOffset;

        ConfigureImage(firstImage);
        if (secondImage != null)
        {
            ConfigureImage(secondImage);

            // Posiciona a segunda imagem aplicando o desconto
            secondImage.anchoredPosition = new Vector2(firstImage.anchoredPosition.x - imageWidth, firstImage.anchoredPosition.y);
        }

        rightBoundary = (Screen.width / 2f) + (firstImage.rect.width / 2f);
    }

    void ConfigureImage(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    void Update()
    {
        // 1. Move a primeira imagem para a direita
        MoveAndCheckBounds(firstImage);

        // 2. Move a segunda imagem para a direita
        if (secondImage != null)
        {
            MoveAndCheckBounds(secondImage);
        }
    }

    void MoveAndCheckBounds(RectTransform img)
    {
        // Move horizontalmente
        img.anchoredPosition += new Vector2(speed * Time.deltaTime, 0);

        // Se a imagem passou completamente do limite da direita da tela...
        if (img.anchoredPosition.x >= rightBoundary)
        {
            // COMPENSAÇÃO DA ARTE: O teletransporte agora empurra a imagem um pouco mais para trás
            // somando o overlapOffset, impedindo que a borda vazia corte o rolo de filme principal!
            float pontoDeTeletransporte = (imageWidth * 2f) + overlapOffset;
            img.anchoredPosition = new Vector2(img.anchoredPosition.x - pontoDeTeletransporte, img.anchoredPosition.y);
        }
    }
}