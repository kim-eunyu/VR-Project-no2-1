using UnityEngine;

// 파일 이름이 SkyboxManager.cs 여야 합니다.
public class SkyboxManager : MonoBehaviour
{
    [Header("시간 설정")]
    [Tooltip("0: 낮, 1: 노을")]
    [Range(0, 1)]
    public float transitionProgress = 0f;

    [Tooltip("0에서 1까지 변하는 데 걸리는 총 시간(초)")]
    public float transitionDuration = 60f;

    [Header("핵심 연결 요소")]
    public Light sunLight;
    [Tooltip("1번 이미지 (낮) 머티리얼")]
    public Material daySkyMaterial;
    [Tooltip("2번 이미지 (저녁) 머티리얼")]
    public Material sunsetSkyMaterial;

    [Header("환경 및 태양 색상 (★0%가 낮, 100%가 노을★)")]
    public Gradient ambientLightColor;
    public Gradient sunLightColor;
    public Gradient fogColor;

    // 씬에서 사용될 머티리얼 복사본
    private Material blendedSkyboxInstance;
    
    // Cloud Seed의 ID를 저장할 변수
    private int cloudSeedID;

    void Start()
    {
        // 필수 머티리얼 체크
        if (daySkyMaterial == null || sunsetSkyMaterial == null)
        {
            Debug.LogError("SkyboxManager: 머티리얼 2개가 모두 설정되지 않았습니다!");
            this.enabled = false; // 스크립트 비활성화
            return;
        }

        // '낮' 머티리얼을 복사하여 씬에 적용 (원본 보호)
        blendedSkyboxInstance = new Material(daySkyMaterial);
        RenderSettings.skybox = blendedSkyboxInstance;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Custom;

        // [수정 1] 셰이더의 실제 내부 이름("_CloudSeed")으로 ID를 찾습니다.
        // (이전 오류 수정: "Cloud Seed" -> "_CloudSeed")
        cloudSeedID = Shader.PropertyToID("_CloudSeed");

        // 시작 상태(0%)로 즉시 적용
        UpdateAllProperties(transitionProgress);
    }

    void Update()
    {
        // progress가 1이 될 때까지 실행
        if (transitionProgress < 1f)
        {
            transitionProgress += Time.deltaTime / transitionDuration;
            transitionProgress = Mathf.Clamp01(transitionProgress);
            UpdateAllProperties(transitionProgress);
        }
    }

    void UpdateAllProperties(float progress)
    {
        // 1. 모든 속성을 섞습니다 (Cloud Seed 값 포함)
        blendedSkyboxInstance.Lerp(daySkyMaterial, sunsetSkyMaterial, progress);

        // 2. [★핵심 수정★] 번쩍임 방지
        //    'Cloud Seed'는 float가 아닌 int(정수) 타입입니다.
        //    GetInt로 'Day' 머티리얼의 값(0)을 읽어와서
        //    SetInt로 덮어써서 구름 모양을 고정시킵니다.
        //    (이전 오류 수정: GetFloat/SetFloat -> GetInt/SetInt)
        blendedSkyboxInstance.SetInt(cloudSeedID, daySkyMaterial.GetInt(cloudSeedID));

        // 3. 태양 및 환경광 변경
        if (sunLight != null)
        {
            float sunAngle = Mathf.Lerp(90f, 180f, progress); 
            sunLight.transform.localRotation = Quaternion.Euler(sunAngle, -30f, 0);
            sunLight.color = sunLightColor.Evaluate(progress);
            sunLight.intensity = Mathf.Lerp(1f, 0f, progress); 
        }

        // 4. 환경광, 안개 색상 변경
        RenderSettings.ambientLight = ambientLightColor.Evaluate(progress);
        RenderSettings.fogColor = fogColor.Evaluate(progress);
    }
}