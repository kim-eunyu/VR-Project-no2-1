using UnityEngine;
using System.Collections; // <-- 추가! 코루틴을 쓰려면 필요해용

[RequireComponent(typeof(AudioSource))]
public class TriggerAudioPlayer : MonoBehaviour
{
    [Header("재생할 오디오 클립")]
    [Tooltip("이 트리거에 들어왔을 때 재생할 오디오 클립을 넣어주세용.")]
    public AudioClip clipToPlay;

    [Header("한 번만 재생할까요?")]
    [Tooltip("체크하면 한 번만 재생되고, 체크 해제하면 들어올 때마다 재생돼용.")]
    public bool playOnce = true;

    [Header("NPC 애니메이터")] // <-- 추가!
    [Tooltip("말하는 애니메이션을 재생할 NPC의 Animator 컴포넌트를 넣어주세용.")]
    public Animator npcAnimator; // <-- 추가!

    [Header("애니메이터 파라미터 이름")] // <-- 추가!
    [Tooltip("Animator에서 '말하기' 상태로 전환하는 bool 파라미터의 이름이에용.")]
    public string talkingParameterName = "isTalking"; // <-- 추가!

    private AudioSource audioSource;
    private bool hasPlayed = false;
    private Coroutine stopTalkingCoroutine; // <-- 추가! 실행 중인 코루틴을 기억해둬용

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. 플레이어 태그 확인
        // 2. 오디오 클립 할당 확인
        // 3. NPC 애니메이터 할당 확인 <-- 추가된 조건!
        if (other.CompareTag("Player") && clipToPlay != null && npcAnimator != null)
        {
            if (playOnce && !hasPlayed)
            {
                PlaySoundAndAnimation(); // <-- 함수 이름 변경!
            }
            else if (!playOnce)
            {
                PlaySoundAndAnimation(); // <-- 함수 이름 변경!
            }
        }
        // NPC 애니메이터가 할당 안 됐을 때 경고 메시지를 띄워줘용 (디버깅용)
        else if (npcAnimator == null)
        {
            Debug.LogWarning("TriggerAudioPlayer: npcAnimator가 할당되지 않았어용!", this.gameObject);
        }
    }

    // 함수 이름을 바꿨어용 (PlaySound -> PlaySoundAndAnimation)
    void PlaySoundAndAnimation() // <-- 수정!
    {
        // 만약 이전에 재생하던 소리/애니메이션이 있다면, 일단 멈추고 새로 시작해용
        if (stopTalkingCoroutine != null)
        {
            StopCoroutine(stopTalkingCoroutine);
        }

        // 1. 오디오 재생
        audioSource.PlayOneShot(clipToPlay);

        // 2. 애니메이션 시작 (Animator의 'isTalking' 파라미터를 true로 변경)
        npcAnimator.SetBool(talkingParameterName, true); // <-- 추가!

        // 3. 오디오 클립 길이가 끝나면 애니메이션을 멈추는 '코루틴'을 시작해용
        stopTalkingCoroutine = StartCoroutine(StopTalkingAfterClip()); // <-- 추가!

        hasPlayed = true;
    }

    // <-- 이 함수 전체가 새로 추가되었어용!
    // 오디오 클립이 끝날 때까지 기다렸다가 애니메이션을 멈추는 코루틴
    private IEnumerator StopTalkingAfterClip()
    {
        // 1. 오디오 클립의 재생 시간(초)만큼 기다려용
        yield return new WaitForSeconds(clipToPlay.length);

        // 2. 시간이 다 되면 애니메이션을 멈춰용 (Animator의 'isTalking' 파라미터를 false로 변경)
        npcAnimator.SetBool(talkingParameterName, false);

        // 3. 코루틴 참조를 비워줘용
        stopTalkingCoroutine = null;
    }
}