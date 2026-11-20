using TMPro;
using UnityEngine;
using UnityEngine.UI;

// S6: 생산시설 단일 부동 창(R17). 도시 작업장 종류(도축/개조/정제)에 따라 해당 작업판만 활성화한다.
[DisallowMultipleComponent]
public sealed class WorkshopWindowView : MonoBehaviour
{
    internal enum WorkshopKind
    {
        None = 0,
        Butchery = 1,
        Modification = 2,
        Refining = 3
    }

    [SerializeField] FloatingWindow floatingWindow;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI descriptionText;
    [SerializeField] TextMeshProUGUI hintText;
    [SerializeField] GameObject butcherySection;
    [SerializeField] GameObject modificationSection;
    [SerializeField] GameObject refiningSection;
    [SerializeField] PuzzleInventoryView butcheryBoardView;
    [SerializeField] PuzzleInventoryView modificationBoardView;
    [SerializeField] PuzzleInventoryView refiningBoardView;
    [SerializeField] Button modeButton;
    [SerializeField] TextMeshProUGUI modeButtonLabel;
    [SerializeField] Button confirmButton;
    [SerializeField] TextMeshProUGUI confirmButtonLabel;

    PuzzleInventoryService session;
    WorkshopKind activeKind;

    public FloatingWindow FloatingWindow => floatingWindow;
    public PuzzleInventoryView ButcheryBoardView => butcheryBoardView;
    public PuzzleInventoryView ModificationBoardView => modificationBoardView;
    public PuzzleInventoryView RefiningBoardView => refiningBoardView;
    public bool IsOpen => floatingWindow && floatingWindow.IsOpen;
    internal bool ShowingButchery => IsOpen && activeKind == WorkshopKind.Butchery;
    internal bool ShowingModification => IsOpen && activeKind == WorkshopKind.Modification;
    internal bool ShowingRefining => IsOpen && activeKind == WorkshopKind.Refining;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (modeButton) modeButton.onClick.AddListener(OnModeClicked);
        if (confirmButton) confirmButton.onClick.AddListener(OnConfirmClicked);
        GameEvents.ButcheryChanged += OnButcheryChanged;
        GameEvents.ModificationChanged += OnModificationChanged;
        GameEvents.RefiningChanged += OnRefiningChanged;
        GameEvents.FundsChanged += OnFundsChanged;
        GameEvents.CityArrived += OnCityArrived;
        PuzzleMapPresenter.TravelAnimatingChanged += OnTravelAnimatingChanged;
        Refresh();
    }

    void OnDisable()
    {
        if (modeButton) modeButton.onClick.RemoveListener(OnModeClicked);
        if (confirmButton) confirmButton.onClick.RemoveListener(OnConfirmClicked);
        GameEvents.ButcheryChanged -= OnButcheryChanged;
        GameEvents.ModificationChanged -= OnModificationChanged;
        GameEvents.RefiningChanged -= OnRefiningChanged;
        GameEvents.FundsChanged -= OnFundsChanged;
        GameEvents.CityArrived -= OnCityArrived;
        PuzzleMapPresenter.TravelAnimatingChanged -= OnTravelAnimatingChanged;
    }

    public void SetOpen(bool _open, bool _persist = true)
    {
        session ??= SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (_open)
        {
            if (PuzzleMapPresenter.IsAnyTravelAnimating)
            {
                return;
            }
            activeKind = ResolveKind(session);
            if (activeKind == WorkshopKind.None)
            {
                return;
            }
            if (floatingWindow)
            {
                floatingWindow.SetOpen(true, _persist);
            }
            else if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            OpenFacilityInSession();
            Refresh();
            return;
        }
        else
        {
            session?.LeaveFacility();
            activeKind = WorkshopKind.None;
        }

        if (floatingWindow)
        {
            floatingWindow.SetOpen(_open, _persist);
        }
        else if (gameObject.activeSelf != _open)
        {
            gameObject.SetActive(_open);
        }
    }

    internal static WorkshopKind ResolveKind(PuzzleInventoryService _session)
    {
        if (_session == null) return WorkshopKind.None;
        if (_session.HasButchery()) return WorkshopKind.Butchery;
        if (_session.HasModification()) return WorkshopKind.Modification;
        if (_session.HasRefining()) return WorkshopKind.Refining;
        return WorkshopKind.None;
    }

    void OpenFacilityInSession()
    {
        if (session == null) return;
        session.LeaveFacility();
        switch (activeKind)
        {
            case WorkshopKind.Butchery:
                if (butcherySection) butcherySection.SetActive(true);
                if (butcheryBoardView) butcheryBoardView.gameObject.SetActive(true);
                if (modificationSection) modificationSection.SetActive(false);
                if (refiningSection) refiningSection.SetActive(false);
                session.OpenButchery();
                break;
            case WorkshopKind.Modification:
                if (butcherySection) butcherySection.SetActive(false);
                if (modificationSection) modificationSection.SetActive(true);
                if (modificationBoardView) modificationBoardView.gameObject.SetActive(true);
                if (refiningSection) refiningSection.SetActive(false);
                session.OpenModification();
                break;
            case WorkshopKind.Refining:
                if (butcherySection) butcherySection.SetActive(false);
                if (modificationSection) modificationSection.SetActive(false);
                if (refiningSection) refiningSection.SetActive(true);
                if (refiningBoardView) refiningBoardView.gameObject.SetActive(true);
                session.OpenRefining();
                break;
        }
    }

    public void Refresh()
    {
        session ??= SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (session == null || !IsOpen)
        {
            return;
        }

        if (butcherySection) butcherySection.SetActive(activeKind == WorkshopKind.Butchery);
        if (modificationSection) modificationSection.SetActive(activeKind == WorkshopKind.Modification);
        if (refiningSection) refiningSection.SetActive(activeKind == WorkshopKind.Refining);

        switch (activeKind)
        {
            case WorkshopKind.Butchery:
                RefreshButchery();
                break;
            case WorkshopKind.Modification:
                RefreshModification();
                break;
            case WorkshopKind.Refining:
                RefreshRefining();
                break;
        }
    }

    void RefreshButchery()
    {
        var action = session.ButcheryAction();
        if (titleText) titleText.text = Presentation.WorkshopWindowTitle(action?.title ?? string.Empty);
        if (descriptionText) descriptionText.text = action?.description ?? string.Empty;
        if (modeButton) modeButton.gameObject.SetActive(false);

        long cost = -1;
        long hours = 0;
        if (session.TryQuoteButchery(out long qFee, out long qHours))
        {
            cost = qFee;
            hours = qHours;
        }
        bool noFunds = cost >= 0 && session.Funds < cost;
        var phase = session.ButcheryPhase();
        if (hintText)
        {
            hintText.text = noFunds
                ? Presentation.PuzzleButcheryInsufficientFunds(cost)
                : Presentation.StationButcheryStatus(phase, session.ButcheryRemainingHours());
        }
        if (confirmButton)
        {
            confirmButton.interactable = cost >= 0 && !noFunds && session.RunStatus == PuzzleRunStatus.InProgress && !PuzzleMapPresenter.IsAnyTravelAnimating;
        }
        if (confirmButtonLabel)
        {
            confirmButtonLabel.text = cost >= 0
                ? Presentation.StationButcheryConfirmCost(cost, hours)
                : Presentation.StationButcheryConfirm;
        }
    }

    void RefreshModification()
    {
        var action = session.ModificationAction();
        if (titleText) titleText.text = Presentation.WorkshopWindowTitle(action?.title ?? string.Empty);
        if (descriptionText) descriptionText.text = action?.description ?? string.Empty;

        var modes = session.ModificationModes();
        if (modeButton)
        {
            modeButton.gameObject.SetActive(modes.Count > 0);
            modeButton.interactable = modes.Count > 0 && session.RunStatus == PuzzleRunStatus.InProgress && !PuzzleMapPresenter.IsAnyTravelAnimating;
        }
        if (modeButtonLabel && modes.Count > 0)
        {
            modeButtonLabel.text = modes[0].displayName;
        }

        long cost = -1;
        long hours = 0;
        if (session.TryQuoteModification(out long qFee, out long qHours))
        {
            cost = qFee;
            hours = qHours;
        }
        bool noFunds = cost >= 0 && session.Funds < cost;
        if (hintText)
        {
            hintText.text = noFunds
                ? Presentation.PuzzleModificationInsufficientFunds(cost)
                : Presentation.PuzzleModificationStatus(session);
        }
        if (confirmButton)
        {
            confirmButton.interactable = cost >= 0 && !noFunds && session.RunStatus == PuzzleRunStatus.InProgress && !PuzzleMapPresenter.IsAnyTravelAnimating;
        }
        if (confirmButtonLabel)
        {
            confirmButtonLabel.text = cost >= 0
                ? Presentation.StationModificationConfirmCost(cost, hours)
                : Presentation.StationModificationConfirm;
        }
    }

    void RefreshRefining()
    {
        var action = session.RefiningAction();
        if (titleText) titleText.text = Presentation.WorkshopWindowTitle(action?.title ?? string.Empty);
        if (descriptionText) descriptionText.text = action?.description ?? string.Empty;
        if (modeButton) modeButton.gameObject.SetActive(false);

        long cost = -1;
        long hours = 0;
        if (session.TryQuoteRefining(out long qFee, out long qHours))
        {
            cost = qFee;
            hours = qHours;
        }
        bool noFunds = cost >= 0 && session.Funds < cost;
        var phase = session.RefiningPhase();
        if (hintText)
        {
            hintText.text = noFunds
                ? Presentation.PuzzleRefiningInsufficientFunds(cost)
                : Presentation.StationRefiningStatus(phase, session.RefiningRemainingHours());
        }
        if (confirmButton)
        {
            confirmButton.interactable = cost >= 0 && !noFunds && session.RunStatus == PuzzleRunStatus.InProgress && !PuzzleMapPresenter.IsAnyTravelAnimating;
        }
        if (confirmButtonLabel)
        {
            confirmButtonLabel.text = cost >= 0
                ? Presentation.StationRefiningConfirmCost(cost, hours)
                : Presentation.StationRefiningConfirm;
        }
    }

    void OnModeClicked()
    {
        if (PuzzleMapPresenter.IsAnyTravelAnimating || session == null || activeKind != WorkshopKind.Modification) return;
        var modes = session.ModificationModes();
        if (modes.Count > 0)
        {
            session.TryToggleModificationMode(modes[0].id, out _);
            Refresh();
        }
    }

    void OnConfirmClicked()
    {
        if (PuzzleMapPresenter.IsAnyTravelAnimating || session == null) return;
        switch (activeKind)
        {
            case WorkshopKind.Butchery:
                if (!session.TryConfirmButchery(out string bReason) && hintText)
                {
                    hintText.text = bReason;
                    return;
                }
                break;
            case WorkshopKind.Modification:
                if (!session.TryConfirmModification(out _, out var failure) && hintText)
                {
                    hintText.text = Presentation.PuzzleModificationReason(failure, session);
                    return;
                }
                break;
            case WorkshopKind.Refining:
                if (!session.TryConfirmRefining(out string rReason) && hintText)
                {
                    hintText.text = rReason;
                    return;
                }
                break;
        }
        Refresh();
    }

    void OnButcheryChanged(GameEvents.ButcheryInfo _info) => Refresh();
    void OnModificationChanged(GameEvents.ModificationInfo _info) => Refresh();
    void OnRefiningChanged(GameEvents.RefiningInfo _info) => Refresh();
    void OnFundsChanged(GameEvents.FundsInfo _info) => Refresh();
    void OnTravelAnimatingChanged(bool _animating) => Refresh();
    void OnCityArrived(GameEvents.CityArrivalInfo _info)
    {
        if (IsOpen)
        {
            SetOpen(false, false);
        }
    }
}
