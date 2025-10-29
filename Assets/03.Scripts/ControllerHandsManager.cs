using UnityEngine;
using UnityEngine.InputSystem;

public class ControllerHandsManager : MonoBehaviour
{
    public InputActionReference triggerActionReference;
    public InputActionReference gripActionReference;

    public Animator handAnimator;

    void Awake()
    {
        handAnimator = GetComponent<Animator>();
        SetupInputActions();
    }

    void OnEnable()
    {
        //�̰� ���ΰ�
/*        if (triggerActionReference != null)
        {
            triggerActionReference.action.Enable();
        }
*/
        triggerActionReference?.action.Enable();
        gripActionReference?.action.Enable();
    }

    void OnDisable()
    {
        triggerActionReference?.action.Disable();
        gripActionReference?.action.Disable();
    }


    void SetupInputActions()
    {
        if (triggerActionReference != null && gripActionReference != null)
        {
            triggerActionReference.action.performed += ctx =>
            UpdateHandsAnimation("Trigger", ctx.ReadValue<float>());
            triggerActionReference.action.canceled += ctx =>
            UpdateHandsAnimation("Trigger", 0);
            gripActionReference.action.performed += ctx =>
            UpdateHandsAnimation("Grip", ctx.ReadValue<float>());
            gripActionReference.action.canceled += ctx =>
            UpdateHandsAnimation("Grip", 0);
        }
        else
        {
            Debug.LogWarning("Input Action Reference are not set in the Inspector");
        }
    }

    void UpdateHandsAnimation(string parameterName, float value)
    {
        if (handAnimator != null)
        {
            handAnimator.SetFloat(parameterName, value);
        }
    }
}
