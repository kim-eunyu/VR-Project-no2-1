using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit.Interactors; // Ray Interactor 사용
using System.IO;                          // 파일 저장/로드 관련
using UnityEngine.SceneManagement; // 씬 관리
using UnityEngine.UI; // UI 사용을 위해 추가
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit.UI; // List 사용을 위해 추가

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
    [Tooltip("스크린샷 UI")]
    [SerializeField] GameObject screenshotUI;

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
    [Tooltip("상호작용 전용 (예: 왼손 x 버튼)")]
    public InputActionProperty leftControllerPrimary; // 왼손 x버튼

    // --- Interactors (Inspector) ---
    [Header("Interactors")]
    [Tooltip("오른손 레이 인터랙터")]
    [SerializeField] XRRayInteractor rightRayInteractor;
    [Tooltip("왼손 레이 인터랙터")]
    [SerializeField] XRRayInteractor leftRayInteractor;

    // --- 스크린샷 갤러리 UI 연결 ---
    [Header("스크린샷 갤러리 UI")]
    [Tooltip("스크린샷이 표시될 8개의 UI Image 컴포넌트")]
    [SerializeField] Image[] screenshotSlots = new Image[8];
    private const int MAX_SLOTS = 8; // 최대 보관 가능 스크린샷 수
    private List<string> allScreenshotFilePaths = new List<string>(); // 실제 저장된 모든 파일 경로를 관리하는 리스트
    private string screenshotsFolderPath; // 스크린샷 폴더 경로
    [Header("스크린샷 확대 보기 UI")]
    [Tooltip("확대된 이미지를 표시할 Image 컴포넌트")]
    [SerializeField] Image fullSizeImageViewer;
    [Tooltip("확대 보기 UI의 최상위 오브젝트 (활성화/비활성화용)")]
    [SerializeField] GameObject fullSizeViewPanel;

    // 스크린샷 썸네일의 부모 오브젝트(버튼) 배열. Inspector에서 연결해야 합니다.
    [Tooltip("각 스크린샷 슬롯의 Button 컴포넌트 (부모 오브젝트에 위치)")]
    [SerializeField] Button[] screenshotButtons = new Button[8];
    // 현재 확대 보기에 사용된 텍스처를 저장하여 메모리 관리를 돕는 변수
    private Texture2D currentFullSizeTexture;

    // --- 상태 변수 ---
    private bool isGrabbingCamera = false; // 현재 카메라 모드인가?
    private bool isTakingScreenshot = false; // 현재 스크린샷 촬영 중인가?
    private bool isScreenshotUIOpen = false;

    // --- Unity Lifecycle Methods ---
    void Awake()
    {
        // 스크린샷 폴더 경로 설정 및 초기 파일 로드
        screenshotsFolderPath = Path.Combine(Application.persistentDataPath, "Screenshots");
        LoadAllScreenshots();

        // **새로 추가된 버튼 이벤트 초기화**
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            int slotIndex = i; // 클로저 이슈 방지를 위해 로컬 변수 사용
            if (screenshotButtons[i] != null)
            {
                // 기존 리스너 모두 제거 (안전성 확보)
                screenshotButtons[i].onClick.RemoveAllListeners(); 
                // 클릭 시 ShowFullSizeScreenshot 함수에 현재 슬롯 인덱스를 전달하며 연결
                screenshotButtons[i].onClick.AddListener(() => ShowFullSizeScreenshot(slotIndex));
            }
        }
    }

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
        if(leftControllerPrimary.action!=null)
        {
            leftControllerPrimary.action.Enable();
            leftControllerPrimary.action.performed += ScreenUIStateControll;
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
        if (leftControllerPrimary.action != null)
        {
            leftControllerPrimary.action.Disable();
            leftControllerPrimary.action.performed -= ScreenUIStateControll;
        }
    }

    // --- 입력 처리 함수들 ---
    
    void ScreenUIStateControll(InputAction.CallbackContext context)
    {
        isScreenshotUIOpen = !isScreenshotUIOpen;
        screenshotUI.GetComponent<Canvas>().enabled = isScreenshotUIOpen;
        screenshotUI.GetComponent<TrackedDeviceGraphicRaycaster>().enabled = isScreenshotUIOpen;
    }

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

            // --- 갤러리 관리 로직 추가 ---
            UpdateScreenshotGallery(filePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"스크린샷 저장 실패: {e.Message}");
        }

        yield return new WaitForSeconds(1.0f);

        isTakingScreenshot = false;

        // Debug.Log("스크린샷 쿨타임 끝");
    }
    
  // --- 갤러리 관리 함수 (수정) ---

    // 앱 시작 시 기존 스크린샷 파일을 모두 로드합니다.
    private void LoadAllScreenshots()
    {
        if (!Directory.Exists(screenshotsFolderPath))
        {
            Directory.CreateDirectory(screenshotsFolderPath);
            return;
        }

        // 폴더 내 모든 PNG 파일 목록을 가져와 리스트에 저장합니다.
        string[] files = Directory.GetFiles(screenshotsFolderPath, "*.png");
        allScreenshotFilePaths = new List<string>(files);
        
        // UI를 갱신합니다.
        RefreshGalleryUI();
    }

    // 새 스크린샷이 저장될 때 전체 파일 리스트를 업데이트합니다.
    private void UpdateScreenshotGallery(string newFilePath)
    {
        // 새 파일 경로를 리스트에 추가합니다. (실제 파일은 삭제하지 않음)
        allScreenshotFilePaths.Add(newFilePath);

        // UI를 갱신합니다.
        RefreshGalleryUI();
    }

// 전체 파일 리스트에서 최신 8개만 가져와 UI Image에 표시합니다.
private void RefreshGalleryUI()
{
    // 1. 모든 슬롯을 초기화합니다. (에디터 에러 방지 포함)
    for (int i = 0; i < MAX_SLOTS; i++)
    {
        if (screenshotSlots[i] != null)
        {
            // 기존 스프라이트 해제 및 투명화
            if (screenshotSlots[i].sprite != null)
            {
                // 에디터 오류 방지 및 메모리 해제 로직
                #if UNITY_EDITOR
                DestroyImmediate(screenshotSlots[i].sprite.texture);
                DestroyImmediate(screenshotSlots[i].sprite);
                #else
                Destroy(screenshotSlots[i].sprite.texture);
                Destroy(screenshotSlots[i].sprite);
                #endif
            }
            screenshotSlots[i].sprite = null;
            screenshotSlots[i].color = new Color(1, 1, 1, 0.2f); // 이미지가 없는 슬롯은 어둡게/투명하게 처리
        }
    }

    // 2. 전체 파일 리스트를 파일 이름 (시간)을 기준으로 오름차순 정렬합니다.
    // [오래된 파일] <--- [최신 파일]
    List<string> sortedPaths = allScreenshotFilePaths
        .OrderBy(p => p)
        .ToList();

    // 3. 가장 최신 파일 8개를 추출합니다. (Skip, Take 사용)
    // 리스트의 끝(가장 최신)에서 8개를 가져옵니다.
    List<string> latestEight = sortedPaths
        .Skip(Mathf.Max(0, sortedPaths.Count - MAX_SLOTS))
        .ToList();
    
    // 4. UI 슬롯 0번부터 최신 스크린샷 순으로 로드합니다.

    // 추출된 latestEight 리스트에는 오래된 순서대로 [file(N-7), ..., fileN]이 들어 있습니다.
    // UI 슬롯 순서 (좌 -> 우): [Slot0, Slot1, ..., Slot7]
    // UI에 로드할 순서: 
    //   - Slot 0: fileN (가장 최신)
    //   - Slot 1: file(N-1)
    //   - ...
    //   - Slot 7: file(N-7) (가장 오래됨)
    
    int latestCount = latestEight.Count;

    // 역순으로 순회하여 가장 최신 파일부터 UI 슬롯 0, 1, 2... 에 할당합니다.
    for (int i = 0; i < latestCount; i++)
    {
        // 파일 인덱스 계산: 가장 최신 파일 (latestCount - 1)부터 역순으로 가져옵니다.
        int fileIndex = (latestCount - 1) - i; 
        
        // UI 슬롯 인덱스는 0, 1, 2, ... 로 순차적으로 증가합니다.
        int targetSlotIndex = i; 

        if (targetSlotIndex < MAX_SLOTS && screenshotSlots[targetSlotIndex] != null)
        {
            string filePath = latestEight[fileIndex];
            
            // i=0: fileIndex=7 (가장 최신 파일) -> Slot 0
            // i=1: fileIndex=6 (두 번째 최신 파일) -> Slot 1
            // ...
            // i=7: fileIndex=0 (가장 오래된 파일) -> Slot 7

            StartCoroutine(LoadImageToSlot(filePath, screenshotSlots[targetSlotIndex]));
        }
    }
}

    // 파일 경로에서 이미지를 로드하여 UI Image에 할당하는 코루틴 (기존과 동일)
    private IEnumerator LoadImageToSlot(string filePath, Image targetImage)
    {
        if (!File.Exists(filePath)) yield break;

        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2);

        if (texture.LoadImage(fileData))
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);

            targetImage.sprite = sprite;
            targetImage.color = Color.white;
        }
        else
        {
            Destroy(texture);
        }

        yield return null;
    }

    // --- 스크린샷 확대/축소 함수 ---

    /// <summary>
    /// 특정 인덱스의 스크린샷 파일을 로드하여 확대 보기에 표시합니다.
    /// 이 함수는 각 버튼의 OnClick 이벤트에 연결될 것입니다.
    /// </summary>
    /// <param name="slotIndex">클릭된 갤러리 슬롯의 인덱스 (0~7).</param>
    public void ShowFullSizeScreenshot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MAX_SLOTS || slotIndex >= allScreenshotFilePaths.Count)
        {
            // Debug.LogWarning($"슬롯 인덱스 {slotIndex}에 유효한 스크린샷이 없습니다.");
            return;
        }

        // 1. 확대 보기 패널 활성화
        if (fullSizeViewPanel != null)
        {
            fullSizeViewPanel.SetActive(true);
        }
        
        // 2. 이전 텍스처 정리 (메모리 누수 방지)
        if (currentFullSizeTexture != null)
        {
            Destroy(currentFullSizeTexture);
        }

        // 3. 표시할 파일 경로 찾기
        
        // RefreshGalleryUI의 로직을 따라, 현재 UI 0번에 해당하는 파일이 무엇인지 계산해야 합니다.
        List<string> sortedPaths = allScreenshotFilePaths
            .OrderBy(p => p)
            .ToList();
        
        List<string> latestEight = sortedPaths
            .Skip(Mathf.Max(0, sortedPaths.Count - MAX_SLOTS))
            .ToList();

        if (latestEight.Count == 0) return;

        // 현재 UI 0~7 슬롯에 해당하는 파일의 인덱스를 계산합니다.
        // latestEight[fileIndex]가 slotIndex에 해당
        
        // i=0: fileIndex=7 (가장 최신 파일) -> Slot 0
        // i=1: fileIndex=6 (두 번째 최신 파일) -> Slot 1
        // ...
        // i=7: fileIndex=0 (가장 오래된 파일) -> Slot 7
        
        int latestCount = latestEight.Count;
        // slotIndex를 파일 리스트의 인덱스로 변환 (slotIndex 0 -> latestEight[latestCount-1])
        int fileIndexInLatestEight = (latestCount - 1) - slotIndex;

        if (fileIndexInLatestEight < 0 || fileIndexInLatestEight >= latestCount)
        {
            // Debug.LogWarning($"계산된 파일 인덱스 {fileIndexInLatestEight}가 유효하지 않습니다.");
            return;
        }

        string filePath = latestEight[fileIndexInLatestEight];

        // 4. 파일 로드 및 확대 보기에 할당
        StartCoroutine(LoadFullSizeImage(filePath));
    }

    // 파일 경로에서 이미지를 로드하여 확대 보기 Image에 할당하는 코루틴
    private IEnumerator LoadFullSizeImage(string filePath)
    {
        if (!File.Exists(filePath) || fullSizeImageViewer == null) yield break;

        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2);

        if (texture.LoadImage(fileData))
        {
            currentFullSizeTexture = texture; // 현재 텍스처 저장
            
            // Sprite로 변환하여 Image에 할당
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);

            // 이전 스프라이트 해제 (메모리 관리)
            if (fullSizeImageViewer.sprite != null)
            {
                Destroy(fullSizeImageViewer.sprite);
            }

            fullSizeImageViewer.sprite = sprite;
            fullSizeImageViewer.color = Color.white;
            
            // Image 크기를 텍스처 종횡비에 맞게 조정하는 코드를 추가할 수 있습니다.
            // 예를 들어, RectTransform의 크기를 조정하거나 Aspect Ratio Fitter 컴포넌트를 사용합니다.
        }
        else
        {
            Destroy(texture);
            // Debug.LogError($"원본 이미지 로드 실패: {filePath}");
        }

        yield return null;
    }

    /// <summary>
    /// 확대 보기 패널을 비활성화하고 메모리를 정리합니다.
    /// </summary>
    public void HideFullSizeScreenshot()
    {
        if (fullSizeViewPanel != null)
        {
            fullSizeViewPanel.SetActive(false);
        }
        
        // 메모리 정리
        if (fullSizeImageViewer != null && fullSizeImageViewer.sprite != null)
        {
            Destroy(fullSizeImageViewer.sprite);
            fullSizeImageViewer.sprite = null;
        }
        if (currentFullSizeTexture != null)
        {
            Destroy(currentFullSizeTexture);
            currentFullSizeTexture = null;
        }
    }
}