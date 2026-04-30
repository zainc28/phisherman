using UnityEngine;
using TMPro;

public class EnemyEmail : MonoBehaviour
{
    [HideInInspector] public bool isScam;
    [HideInInspector] public TowerDefenseManager manager;

    private bool isDead = false;
    private Vector3 towerPos;
    private float speed;
    private float wobbleOffset;

    public void Init(bool scam, string label, float spd, TowerDefenseManager mgr, Vector3 tower)
    {
        isScam = scam;
        speed = spd;
        manager = mgr;
        towerPos = tower;
        wobbleOffset = Random.Range(0f, Mathf.PI * 2f);

        TextMeshProUGUI tmp = GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = label;
    }

    void Update()
    {
        if (isDead) return;
        float wobbleY = Mathf.Sin(Time.time * 2f + wobbleOffset) * 0.12f;
        transform.position += new Vector3(speed * Time.deltaTime, wobbleY * Time.deltaTime, 0);
        if (transform.position.x >= towerPos.x - 0.8f)
            ReachedTower();
    }

    public void GetClicked()
    {
        if (isDead) return;
        isDead = true;
        if (isScam) manager.OnScamDestroyed(transform.position);
        else manager.OnSafeDestroyed(transform.position);
        Destroy(gameObject);
    }

    void ReachedTower()
    {
        if (isDead) return;
        isDead = true;
        if (isScam) manager.OnScamReachedTower();
        Destroy(gameObject);
    }
}