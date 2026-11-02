using UnityEngine;

public class NoiseOnImpact : MonoBehaviour
{
    public AudioClip shatterSound;
    bool used;

    void OnCollisionEnter(Collision c)
    {
        if (used) return;
        used = true;

        if (shatterSound != null)
            AudioSource.PlayClipAtPoint(shatterSound, transform.position);

        GameManager.Instance.enemy.HearNoise(transform.position);
        Destroy(gameObject);
    }
}
