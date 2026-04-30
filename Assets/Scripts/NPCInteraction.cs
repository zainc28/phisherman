using UnityEngine;
using UnityEngine.UI;

public class NPCInteraction : MonoBehaviour
{
    public GameObject talkPrompt;
    public float interactRange = 1.5f;
    private Transform player;

    void Start()
    {
        player = GameObject.Find("Phisherman").transform;
        talkPrompt.SetActive(false);
    }

    void Update()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance < interactRange)
        {
            talkPrompt.SetActive(true);
        }
        else
        {
            talkPrompt.SetActive(false);
        }
    }
}