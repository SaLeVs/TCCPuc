using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    [SerializeField] private AudioSource musicSource;
    
    [SerializeField] private AudioClip[] menuTracks;
    [SerializeField] private AudioClip[] gameTracks;

    private AudioClip[] currentTracks;
    private Coroutine musicCoroutine;
    private static MusicManager instance;


    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        currentTracks = null;
        SceneManager.sceneLoaded += SceneManagerOnSceneLoaded;
        SceneManagerOnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }
    
    private void SceneManagerOnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
    {
        AudioClip[] chosenTracks;

        if (scene.name == "MainMenu")
        {
            chosenTracks = menuTracks;
        }
        else if (scene.name == "Game" || scene.name == "Lobby")
        {
            chosenTracks = gameTracks;
        }
        else
        {
            chosenTracks = null;
        }

        if (chosenTracks == currentTracks)
            return;

        currentTracks = chosenTracks;

        if (musicCoroutine != null)
            StopCoroutine(musicCoroutine);

        if (currentTracks == null || currentTracks.Length == 0)
            return;

        musicCoroutine = StartCoroutine(PlayMusicsOnScene(currentTracks));
    }

    private IEnumerator PlayMusicsOnScene(AudioClip[] musicTracks)
    {
        while (true)
        {
            if (musicTracks == null || musicTracks.Length == 0)
            {
                yield break;
            }

            int musicIndex = UnityEngine.Random.Range(0, musicTracks.Length);
            musicSource.clip = musicTracks[musicIndex];
            musicSource.Play();

            yield return new WaitForSeconds(musicSource.clip.length);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneManagerOnSceneLoaded;
    }
}