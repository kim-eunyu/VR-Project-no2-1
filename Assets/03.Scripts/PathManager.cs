using UnityEngine;
using UnityEngine.Splines;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XRSimpleInteractable 사용

// (이 스크립트는 씬의 PathManager 오브젝트에 붙어있어야 합니다)
public class PathManager : MonoBehaviour
{
    // --- 씬 오브젝트 연결 (Inspector) ---
    [Header("씬 오브젝트 연결 (Inspector)")]
    [Tooltip("경로를 따라 움직일 오브젝트 (예: Speed_Boat)")]
    public SplineAnimate objectToMove;

    [Tooltip("상호작용 및 애니메이션을 제어할 NPC 게임 오브젝트")]
    public GameObject npc;

    [Tooltip("경로(SplineContainer)들을 순서대로 담은 리스트")]
    public List<SplineContainer> pathContainers;
    public GameObject skyManger;

    // [Tooltip("회전시킬 Directional Light")]
    // public Light directionalLight;

    [Tooltip("BGM을 재생할 AudioSource 컴포넌트")]
    public AudioSource bgmPlayer;

    [Tooltip("경로 선택 후 숨길 UI 그룹 (예: SelectUI)")]
    [SerializeField] private GameObject selectUI;

    // --- 경로별 설정값 (Inspector 리스트) ---
    [Header("경로별 설정값 (리스트)")]
    [Tooltip("라이트 회전 애니메이션 지속 시간 (초)")]
    public float lightAnimationDuration = 2f;
    // [Tooltip("경로 순서에 맞는 스카이박스 머티리얼 리스트")]
    // public List<Material> skyboxMaterials;
    [Tooltip("경로 순서에 맞는 BGM 오디오 클립 리스트")]
    public List<AudioClip> bgmClips;
    // [Tooltip("경로 순서에 맞는 라이트의 목표 X축 회전값 리스트")]
    // public List<float> lightRotations;

    [SerializeField] AudioSource boatSound;

    // --- Unity Lifecycle Methods ---
    void Awake()
    {
        // 게임 시작 시 UI가 보이도록 보장
        if (selectUI != null)
        {
            selectUI.SetActive(true);
        }
    }

    // --- Public Methods ---

    /// <summary>
    /// UI 버튼의 OnClick 이벤트에서 호출될 함수 (인자로 0, 1, 2 전달)
    /// </summary>
    public void SelectPath(int pathIndex)
    {
        // 1. 필수 오브젝트 연결 확인
        if (objectToMove == null)
        {
            // Debug.LogError("움직일 오브젝트(Object To Move)가 Inspector에 연결되지 않았습니다.");
            return;
        }
        if (pathContainers == null || pathContainers.Count == 0)
        {
            // Debug.LogError("경로 컨테이너 리스트(Path Containers)가 비어있습니다.");
            return;
        }

        // 2. 경로 인덱스 유효성 검사
        if (pathIndex < 0 || pathIndex >= pathContainers.Count)
        {
            // Debug.LogWarning($"잘못된 경로 인덱스({pathIndex})입니다. 리스트에는 {pathContainers.Count}개의 경로만 있습니다.");
            return;
        }

        // 3. 선택된 경로 컨테이너 유효성 검사
        SplineContainer selectedContainer = pathContainers[pathIndex];
        if (selectedContainer == null)
        {
            // Debug.LogError($"리스트의 {pathIndex}번째 경로 컨테이너가 비어있습니다(null).");
            return;
        }

        // Debug.Log($"경로 {pathIndex}번 (컨테이너: {selectedContainer.name})을 선택했습니다.");

        selectUI.SetActive(false);

        // --- 환경 변경 함수 호출 ---
        // TriggerSkyboxChange(pathIndex); // 필요시 주석 해제
        TriggerBGMChange(pathIndex);
        boatSound.Play();
        // TriggerLightChange(pathIndex); // 라이트 변경 코루틴 시작 (UI 숨김 포함)

        // --- 핵심 로직: SplineAnimate 설정 및 실행 ---
        // SplineAnimate 컴포넌트가 참조할 SplineContainer를 선택된 것으로 변경
        objectToMove.Container = selectedContainer;
        // 애니메이션을 처음부터 다시 시작 (true: 즉시 시작)
        objectToMove.Restart(true);

        npc.SetActive(true);

        skyManger.GetComponent<SkyboxManager>().enabled = true;
    }

    // 스카이박스 변경
    // private void TriggerSkyboxChange(int index)
    // {
    //     if (index >= skyboxMaterials.Count || skyboxMaterials[index] == null)
    //     {
    //         Debug.LogWarning($"SkyboxMaterials 리스트에 {index}번 항목이 없거나 null입니다.");
    //         return;
    //     }
    //     RenderSettings.skybox = skyboxMaterials[index];
    //     DynamicGI.UpdateEnvironment(); // 환경광 업데이트
    // }

    // BGM 변경
    
    private void TriggerBGMChange(int index)
    {
        if (bgmPlayer == null)
        {
            // Debug.LogWarning("BGM Player(AudioSource)가 Inspector에 연결되지 않았습니다.");
            return;
        }
        if (index >= bgmClips.Count || bgmClips[index] == null)
        {
            // Debug.LogWarning($"BGM Clips 리스트에 {index}번 항목이 없거나 null입니다.");
            return;
        }
        bgmPlayer.clip = bgmClips[index];
        bgmPlayer.Play();
    }

    // // 라이트 회전 애니메이션 시작
    // private void TriggerLightChange(int index)
    // {
    //     if (directionalLight == null)
    //     {
    //         Debug.LogWarning("Directional Light가 Inspector에 연결되지 않았습니다.");
    //         return;
    //     }
    //     if (index >= lightRotations.Count)
    //     {
    //         Debug.LogWarning($"Light Rotations 리스트에 {index}번 항목이 없습니다.");
    //         return;
    //     }

    //     // 이전 코루틴 중지
    //     if (m_LightCoroutine != null)
    //     {
    //         StopCoroutine(m_LightCoroutine);
    //     }
    //     // 새 코루틴 시작
    //     float targetXRotation = lightRotations[index];
    //     m_LightCoroutine = StartCoroutine(AnimateLightRotation(targetXRotation, lightAnimationDuration));
    // }

    // // 라이트 회전 코루틴
    // private IEnumerator AnimateLightRotation(float targetX, float duration)
    // {
    //     if (duration <= 0) // duration이 0 이하면 즉시 설정
    //     {
    //         if (directionalLight != null) directionalLight.transform.rotation = Quaternion.Euler(targetX, directionalLight.transform.eulerAngles.y, directionalLight.transform.eulerAngles.z);
    //         if (selectUI != null) selectUI.SetActive(false);
    //         m_LightCoroutine = null;
    //         yield break; // 코루틴 종료
    //     }

    //     float elapsedTime = 0f;
    //     Quaternion startRotation = directionalLight.transform.rotation;
    //     Quaternion targetRotation = Quaternion.Euler(targetX, startRotation.eulerAngles.y, startRotation.eulerAngles.z);

    //     while (elapsedTime < duration)
    //     {
    //         float t = elapsedTime / duration;
    //         // 시간에 따라 Slerp 보간으로 부드럽게 회전
    //         directionalLight.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
    //         elapsedTime += Time.deltaTime;
    //         yield return null; // 다음 프레임까지 대기
    //     }

    //     // 애니메이션 완료 후 정확한 값으로 설정
    //     directionalLight.transform.rotation = targetRotation;
    //     // UI 숨김
    //     if (selectUI != null)
    //     {
    //         selectUI.SetActive(false);
    //     }
    //     m_LightCoroutine = null; // 코루틴 완료 플래그
    // }
}