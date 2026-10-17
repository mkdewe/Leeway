using RootMotion.Demos;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HandAnchorsSettings : MonoBehaviour
{
    public DynamicAnchor leftDynamicAnchor;
    public DynamicAnchor rightDynamicAnchor;

    public Slider massSlider;
    public TMP_Text massSliderValueText; // Tekst dla masy
    public Slider velocityMassSlider;
    public TMP_Text velocityMassSliderValueText; // Tekst dla velocity mass
    public Slider distanceThresholdSlider;
    public TMP_Text distanceThresholdSliderValueText; // Tekst dla progu dystansu
    public Slider velocityThresholdSlider;
    public TMP_Text velocityThresholdSliderValueText; // Tekst dla velocity threshold
    public Toggle trackDivergenceToggle;


    private void Update()
    {
        if (!leftDynamicAnchor && !rightDynamicAnchor)
        {
            leftDynamicAnchor = GameObject.Find("DynamicAnchor Left Hand").GetComponent<DynamicAnchor>();
            rightDynamicAnchor = GameObject.Find("DynamicAnchor Right Hand").GetComponent<DynamicAnchor>();
            if (leftDynamicAnchor && rightDynamicAnchor) InitializeSliders(leftDynamicAnchor);
        }
    }

    private void Start()
    {
        // Dodajemy nas³uchiwacze zmian w UI
        massSlider.onValueChanged.AddListener(OnMassChanged);
        velocityMassSlider.onValueChanged.AddListener(OnVelocityMassChanged);
        distanceThresholdSlider.onValueChanged.AddListener(OnDistanceThresholdChanged);
        velocityThresholdSlider.onValueChanged.AddListener(OnVelocityThresholdChanged);
        trackDivergenceToggle.onValueChanged.AddListener(OnTrackDivergenceToggled);
    }

    // Funkcja do inicjalizacji suwaków (zak³adamy, ¿e obie rêce maj¹ takie same ustawienia pocz¹tkowe)
    private void InitializeSliders(DynamicAnchor dynamicAnchor)
    {
        massSlider.value = dynamicAnchor.mass;
        massSliderValueText.text = dynamicAnchor.mass.ToString("F2"); // Ustawiamy pocz¹tkow¹ wartoœæ tekstu

        velocityMassSlider.value = dynamicAnchor.velocityMassAdd;
        velocityMassSliderValueText.text = dynamicAnchor.velocityMassAdd.ToString("F2"); // Ustawiamy pocz¹tkow¹ wartoœæ tekstu

        distanceThresholdSlider.value = dynamicAnchor.DivergenceDistanceThreshold.magnitude;
        distanceThresholdSliderValueText.text = dynamicAnchor.DivergenceDistanceThreshold.magnitude.ToString("F2"); // Ustawiamy pocz¹tkow¹ wartoœæ tekstu

        velocityThresholdSlider.value = dynamicAnchor.DivergenceForceThreshold;
        velocityThresholdSliderValueText.text = dynamicAnchor.DivergenceForceThreshold.ToString("F2"); // Ustawiamy pocz¹tkow¹ wartoœæ tekstu

        trackDivergenceToggle.isOn = dynamicAnchor.TrackDivergenceDistance;
    }

    // Aktualizuje wartoœæ dla obu DynamicAnchor jednoczeœnie i tekstu obok suwaka
    public void OnMassChanged(float value)
    {
        leftDynamicAnchor.mass = value;
        rightDynamicAnchor.mass = value;
        massSliderValueText.text = value.ToString("F2"); // Aktualizuje tekst z wartoœci¹
    }

    public void OnVelocityMassChanged(float value)
    {
        leftDynamicAnchor.velocityMassAdd = value;
        rightDynamicAnchor.velocityMassAdd = value;
        velocityMassSliderValueText.text = value.ToString("F2"); // Aktualizuje tekst z wartoœci¹
    }

    public void OnDistanceThresholdChanged(float value)
    {
        Vector3 newThreshold = Vector3.one * value;
        leftDynamicAnchor.DivergenceDistanceThreshold = newThreshold;
        rightDynamicAnchor.DivergenceDistanceThreshold = newThreshold;
        distanceThresholdSliderValueText.text = value.ToString("F2"); // Aktualizuje tekst z wartoœci¹
    }

    public void OnVelocityThresholdChanged(float value)
    {
        leftDynamicAnchor.DivergenceForceThreshold = value;
        rightDynamicAnchor.DivergenceForceThreshold = value;
        velocityThresholdSliderValueText.text = value.ToString("F2"); // Aktualizuje tekst z wartoœci¹
    }

    public void OnTrackDivergenceToggled(bool value)
    {
        leftDynamicAnchor.TrackDivergenceDistance = value;
        rightDynamicAnchor.TrackDivergenceDistance = value;
    }
}
