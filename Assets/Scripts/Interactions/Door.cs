using UnityEngine;

public class Door : MonoBehaviour
{
    [Header("Door State")]
    public bool isOpen = false;        // Current door state

    [Header("Door Movement")]
    public float openAngle = 90f;      // Angle to open
    public float openSpeed = 2f;       // Rotation speed

    [Header("Door Sounds")]
    public AudioClip openSound;        // Sound when door opens
    public AudioClip closeSound;       // Sound when door closes
    public AudioSource audioSource;    // Audio source to play sounds

    private Quaternion closedRotation; // Original closed rotation
    private Quaternion targetOpenRotation;

    void Start()
    {
        closedRotation = transform.rotation;
        targetOpenRotation = Quaternion.Euler(transform.eulerAngles + new Vector3(0, openAngle, 0));

        // Auto-add an AudioSource if missing
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    /// <summary>
    /// Opens the door (only if currently closed)
    /// </summary>
    public void OpenDoor()
    {
        if (!isOpen)
        {
            isOpen = true;
            StopAllCoroutines();
            StartCoroutine(RotateDoor(targetOpenRotation));

            PlaySound(openSound);
        }
    }

    /// <summary>
    /// Closes the door (only if currently open)
    /// </summary>
    public void CloseDoor()
    {
        if (isOpen)
        {
            isOpen = false;
            StopAllCoroutines();
            StartCoroutine(RotateDoor(closedRotation));

            PlaySound(closeSound);
        }
    }

    /// <summary>
    /// Toggle between open and closed states.
    /// </summary>
    public void ToggleDoor()
    {
        StopAllCoroutines();
        if (isOpen)
        {
            isOpen = false;
            StartCoroutine(RotateDoor(closedRotation));
            PlaySound(closeSound);
        }
        else
        {
            isOpen = true;
            StartCoroutine(RotateDoor(targetOpenRotation));
            PlaySound(openSound);
        }
    }

    /// <summary>
    /// Smoothly rotates the door to the target rotation.
    /// </summary>
    private System.Collections.IEnumerator RotateDoor(Quaternion targetRotation)
    {
        while (Quaternion.Angle(transform.rotation, targetRotation) > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * openSpeed);
            yield return null;
        }

        // Snap to final rotation
        transform.rotation = targetRotation;
    }

    /// <summary>
    /// Plays a given sound effect, if assigned.
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}
