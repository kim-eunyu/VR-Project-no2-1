using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit.Interactors; // Ray Interactor 사용
using System.IO;                          // 파일 저장/로드 관련
using UnityEngine.SceneManagement; // 씬 관리

// (이 스크립트는 XR Origin 또는 Player 오브젝트에 붙어있어야 합니다)
public class PlayerController : MonoBehaviour
{
    // --- 오브젝트 연결 (Inspector) ---
    [Header("오브젝트 연결")]
    [Tooltip("카메라 모드 시 활성화될 카메라 오브젝트")]
    [SerializeField] GameObject cameraObject;
    [Tooltip("손 모드 시 활성화될 오른손 모델 오브젝트")]
    [SerializeField] GameObject rightHandObject; // 오른손 모델
    [Tooltip("스크린샷 촬영에 사용될 카메라 컴포넌트")]
    [SerializeField] Camera cameraComponent;
    [Tooltip("스크린샷 촬영 결과가 저장될 렌더 텍스처")]
    [SerializeField] RenderTexture targetRenderTexture;

    // --- 컨트롤러 입력 (Input Actions Asset) ---
    [Header("컨트롤러 입력")]
    [Tooltip("카메라 모드 전환 (예: 오른손 A 버튼)")]
    public InputActionProperty rightControllerPrimary; // A 버튼
    [Tooltip("씬 재시작 (예: 오른손 B 버튼)")]
    public InputActionProperty rightControllerSecondary; // B 버튼
    [Tooltip("스크린샷 촬영 또는 상호작용 (예: 오른손 트리거)")]
    public InputActionProperty rightControllerTrigger; // 오른손 트리거
    [Tooltip("상호작용 전용 (예: 왼손 트리거)")]
    public InputActionProperty leftControllerTrigger;  // 왼손 트리거

    // --- Interactors (Inspector) ---
    [Header("Interactors")]
    [Tooltip("오른손 레이 인터랙터")]
    [SerializeField] XRRayInteractor rightRayInteractor;
    [Tooltip("왼손 레이 인터랙터")]
    [SerializeField] XRRayInteractor leftRayInteractor;

    // --- 상태 변수 ---
    private bool isGrabbingCamera = false; // 현재 카메라 모드인가?
    private bool isTakingScreenshot = false; // 현재 스크린샷 촬영 중인가?

    // --- Unity Lifecycle Methods ---
    void OnEnable()
    {
        // 입력 액션 활성화 및 이벤트 구독
        if (rightControllerPrimary.action != null)
        {
            rightControllerPrimary.action.Enable();
            rightControllerPrimary.action.performed += ToggleCameraMode;
        }
        if (rightControllerTrigger.action != null)
        {
            rightControllerTrigger.action.Enable();
            rightControllerTrigger.action.performed += OnRightTriggerPressed;
        }
        if (leftControllerTrigger.action != null)
        {
            leftControllerTrigger.action.Enable();
            leftControllerTrigger.action.performed += OnLeftTriggerPressed;
        }
        if (rightControllerSecondary.action != null)
        {
            rightControllerSecondary.action.Enable();
            rightControllerSecondary.action.performed += RestartGameMode; // B 버튼 -> RestartGameMode 직접 호출
        }
    }

    void OnDisable()
    {
        // 입력 액션 비활성화 및 이벤트 구독 해지
        if (rightControllerPrimary.action != null)
        {
            rightControllerPrimary.action.performed -= ToggleCameraMode;
            rightControllerPrimary.action.Disable();
        }
        if (rightControllerTrigger.action != null)
        {
            rightControllerTrigger.action.performed -= OnRightTriggerPressed;
            rightControllerTrigger.action.Disable();
        }
        if (leftControllerTrigger.action != null)
        {
            leftControllerTrigger.action.performed -= OnLeftTriggerPressed;
            leftControllerTrigger.action.Disable();
        }
        if (rightControllerSecondary.action != null)
        {
            rightControllerSecondary.action.Disable();
            rightControllerSecondary.action.performed -= RestartGameMode;
        }
    }

    // --- 입력 처리 함수들 ---

    // 카메라 모드 <-> 손 모드 전환
    void ToggleCameraMode(InputAction.CallbackContext context)
    {
        isGrabbingCamera = !isGrabbingCamera;
        cameraObject.SetActive(isGrabbingCamera);
        rightHandObject.SetActive(!isGrabbingCamera);
    }

    // 씬 재시작 (B 버튼) - NullReferenceException 발생 지점
    void RestartGameMode(InputAction.CallbackContext context)
    {
        // Debug.Log("GameScene을 다시 로드합니다...");
        // 이 코드가 XR Hands 패키지와 충돌하여 NRE를 유발합니다.
        SceneManager.LoadScene("GameScene"); // 현재 씬 이름을 정확히 입력하세요.
    }

    // 오른손 트리거 눌렸을 때
    void OnRightTriggerPressed(InputAction.CallbackContext context)
    {
        if (isGrabbingCamera) // 카메라 모드일 때 스크린샷
        {
            if (!isTakingScreenshot)
            {
                StartCoroutine(CaptureScreenshotCoroutine());
            }
        }
        // else // 손 모드일 때 상호작용 시도
        // {
        //     CheckInteractorHit(rightRayInteractor);
        // }
    }

    // 왼손 트리거 눌렸을 때
    void OnLeftTriggerPressed(InputAction.CallbackContext context)
    {
        // 왼손은 항상 상호작용 시도
        // CheckInteractorHit(leftRayInteractor);
    }

    // --- 상호작용 및 스크린샷 로직 ---

    // 레이 인터랙터가 가리키는 대상 확인 및 NPC 상호작용
    // void CheckInteractorHit(XRRayInteractor interactor)
    // {
    //     if (interactor == null)
    //     {
    //         Debug.LogWarning("Interactor가 할당되지 않았습니다.");
    //         return;
    //     }

    //     // 현재 레이캐스트 충돌 정보 가져오기
    //     if (interactor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
    //     {
    //         // 충돌한 오브젝트의 레이어 마스크 확인 및 Capsule Collider 존재 여부 확인
    //         // (Interactor의 Interaction Layer Mask와 NPC의 Layer가 일치해야 함)
    //         if (((1 << hit.collider.gameObject.layer) & interactor.interactionLayers) != 0 &&
    //             hit.collider.GetComponent<CapsuleCollider>() != null)
    //         {
    //             Debug.Log($"Interactor '{interactor.name}'가 NPC '{hit.collider.gameObject.name}'에 닿았습니다.");
    //             TriggerNPCInteraction(hit.collider.gameObject); // NPC 상호작용 함수 호출
    //         }
    //         else
    //         {
    //             // Debug.Log($"Interactor '{interactor.name}'가 '{hit.collider.gameObject.name}' (Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)})에 닿았지만 상호작용 대상이 아닙니다.");
    //         }
    //     }
    //     else
    //     {
    //         // Debug.Log($"Interactor '{interactor.name}'가 아무것도 가리키고 있지 않습니다.");
    //     }
    // }

    // // NPC와 상호작용 (애니메이션 트리거)
    // void TriggerNPCInteraction(GameObject npcObject)
    // {
    //     Debug.Log($"NPC '{npcObject.name}'와(과) 상호작용!");
    //     Animator animator = npcObject.GetComponent<Animator>();
    //     if (animator != null)
    //     {
    //         animator.SetTrigger("Talking"); // "Talking" 트리거 파라미터가 Animator에 있어야 함
    //     }
    //     else
    //     {
    //         Debug.LogWarning($"'{npcObject.name}'에서 Animator 컴포넌트를 찾을 수 없습니다.");
    //     }
    // }

    // 스크린샷 촬영 및 저장 코루틴
    IEnumerator CaptureScreenshotCoroutine()
    {
        if (cameraComponent == null || targetRenderTexture == null)
        {
            // Debug.LogError("카메라 또는 렌더 텍스처가 할당되지 않았습니다.");
            yield break; // 코루틴 중단
        }

        isTakingScreenshot = true;
        // Debug.Log("스크린샷 촬영 시작...");
        // 렌더링이 완료된 후 실행되도록 프레임 끝까지 대기
        yield return new WaitForEndOfFrame();

        // 활성 렌더 텍스처를 백업하고 타겟 렌더 텍스처로 설정
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = targetRenderTexture;

        // 렌더 텍스처 크기와 동일한 Texture2D 생성
        Texture2D screenshotTexture = new Texture2D(targetRenderTexture.width, targetRenderTexture.height, TextureFormat.RGB24, false);
        // 활성 렌더 텍스처의 픽셀 읽기
        screenshotTexture.ReadPixels(new Rect(0, 0, targetRenderTexture.width, targetRenderTexture.height), 0, 0);
        screenshotTexture.Apply(); // 텍스처 변경사항 적용

        // 활성 렌더 텍스처 복원
        RenderTexture.active = currentRT;
        // Texture2D를 PNG 바이트 배열로 인코딩
        byte[] bytes = screenshotTexture.EncodeToPNG();
        // 메모리 절약을 위해 임시 Texture2D 파괴
        Destroy(screenshotTexture);

        // 저장 경로 설정 (애플리케이션 데이터 경로 아래 Screenshots 폴더)
        string folderPath = Path.Combine(Application.persistentDataPath, "Screenshots");
        // 폴더가 없으면 생성
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        // 파일 이름 생성 (날짜와 시간 포함)
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"Screenshot_{timestamp}.png";
        string filePath = Path.Combine(folderPath, fileName);

        // 바이트 배열을 파일로 저장
        try
        {
            File.WriteAllBytes(filePath, bytes);
            Debug.Log($"스크린샷 저장 완료: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"스크린샷 저장 실패: {e.Message}");
        }

        yield return new WaitForSeconds(1.0f);

        isTakingScreenshot = false;

        // Debug.Log("스크린샷 쿨타임 끝");
    }
}