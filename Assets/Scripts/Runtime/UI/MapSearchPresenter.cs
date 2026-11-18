using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MapSearchPresenter : MonoBehaviour
{
    [SerializeField] MapSearchView view;

    List<SearchEntry> searchEntries;
    List<SearchEntry> currentMatches;
    bool isFocused;

    void Awake()
    {
        if (view == null)
        {
            view = GetComponent<MapSearchView>();
        }
    }

    void OnEnable()
    {
        if (view == null)
        {
            view = GetComponent<MapSearchView>();
        }

        if (view != null)
        {
            view.QueryChanged += OnQueryChanged;
            view.QuerySubmitted += OnQuerySubmitted;
            view.SlotSelected += OnSlotSelected;
            view.RouteClicked += OnRouteClicked;
            view.InputFocused += OnInputFocused;
            view.InputUnfocused += OnInputUnfocused;
        }
    }

    void OnDisable()
    {
        if (view != null)
        {
            view.QueryChanged -= OnQueryChanged;
            view.QuerySubmitted -= OnQuerySubmitted;
            view.SlotSelected -= OnSlotSelected;
            view.RouteClicked -= OnRouteClicked;
            view.InputFocused -= OnInputFocused;
            view.InputUnfocused -= OnInputUnfocused;
            view.HideResults();
        }

        if (isFocused)
        {
            isFocused = false;
            GameEvents.RaiseTextInputFocusChanged(new GameEvents.TextInputFocusInfo(gameObject.scene.handle, false));
        }

        currentMatches = null;
    }

    public bool TryConsumeCancel()
    {
        if (view != null && (view.IsInputFocused || view.IsResultOpen))
        {
            view.ClearAndClose();
            return true;
        }
        return false;
    }

    void OnQueryChanged(string _query)
    {
        if (string.IsNullOrWhiteSpace(_query))
        {
            currentMatches = null;
            if (view != null)
            {
                view.HideResults();
            }
            return;
        }

        EnsureSearchEntries();
        // 전장의 안개: 지도에 없는 역은 찾지 않는다. 지역 이름은 처음부터 안다.
        var service = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        var entries = service == null ? searchEntries : searchEntries
            .Where(_entry => _entry.kind != EntityLinkKind.City ||
                service.CityVisibility(_entry.id) != PuzzleMapVisibility.Hidden)
            .ToList();
        currentMatches = KoreanSearch.Search(entries, _query, MapSearchView.SLOT_COUNT, out int total);

        string currentCityId = GetCurrentCityId();
        if (view != null)
        {
            view.ShowResults(currentMatches, total, currentCityId);
        }
    }

    void OnQuerySubmitted(string _query)
    {
        if (currentMatches != null && currentMatches.Count > 0)
        {
            OnSlotSelected(0);
        }
    }

    void OnSlotSelected(int _index)
    {
        if (currentMatches == null || _index < 0 || _index >= currentMatches.Count)
        {
            return;
        }

        var entry = currentMatches[_index];
        string cityId = entry.kind == EntityLinkKind.City ? entry.id : null;
        string regionId = entry.kind == EntityLinkKind.Region ? entry.id : null;

        GameEvents.RaiseMapFocusRequested(new GameEvents.MapFocusInfo(gameObject.scene.handle, cityId, regionId));

        if (view != null)
        {
            view.ClearAndClose();
        }
    }

    void OnRouteClicked(int _index)
    {
        if (currentMatches == null || _index < 0 || _index >= currentMatches.Count)
        {
            return;
        }

        var entry = currentMatches[_index];
        if (entry.kind == EntityLinkKind.City)
        {
            GameEvents.RaiseRoutePlanRequested(new GameEvents.RoutePlanRequestInfo(gameObject.scene.handle, entry.id));

            if (view != null)
            {
                view.ClearAndClose();
            }
        }
    }

    void OnInputFocused()
    {
        isFocused = true;
        GameEvents.RaiseTextInputFocusChanged(new GameEvents.TextInputFocusInfo(gameObject.scene.handle, true));
    }

    void OnInputUnfocused()
    {
        if (isFocused)
        {
            isFocused = false;
            GameEvents.RaiseTextInputFocusChanged(new GameEvents.TextInputFocusInfo(gameObject.scene.handle, false));
        }
    }

    void EnsureSearchEntries()
    {
        if (searchEntries != null && searchEntries.Count > 0)
        {
            return;
        }

        searchEntries = new List<SearchEntry>();
        var tables = TableManager.Instance;
        if (tables == null)
        {
            return;
        }

        var visitedRegions = new HashSet<string>(StringComparer.Ordinal);
        if (tables.Cities != null)
        {
            foreach (var city in tables.Cities)
            {
                if (city == null || string.IsNullOrEmpty(city.id))
                {
                    continue;
                }

                searchEntries.Add(new SearchEntry(EntityLinkKind.City, city.id, city.displayName ?? city.id));

                if (!string.IsNullOrEmpty(city.regionId) && visitedRegions.Add(city.regionId))
                {
                    if (tables.TryGetRegion(city.regionId, out var region) && region != null)
                    {
                        searchEntries.Add(new SearchEntry(EntityLinkKind.Region, region.id, region.displayName ?? region.id));
                    }
                }
            }
        }
    }

    string GetCurrentCityId()
    {
        var service = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        return service != null ? service.CurrentCityId : null;
    }
}
