using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PuzzleInventory
{
    readonly IReadOnlyDictionary<string, PuzzleItemDefinition> definitions;
    readonly IReadOnlyList<PuzzleSynergyRuleData> synergyRules;
    // 규칙별 숙련 보강치(%p) 조회 훅. 세션 상태는 저장하지 않고 계산 시점에 세션이 준 조회자를 빌려 쓴다.
    internal Func<PuzzleSynergyRuleData, decimal> SynergyComboBonusPercent { get; set; }
    PuzzleInventoryValuation valuation = new();
    readonly SortedDictionary<int, PuzzlePlacement> placements = new();
    readonly Dictionary<Vector2Int, (int InstanceId, PuzzleCell Cell)> occupancy = new();
    readonly HashSet<Vector2Int> blockedCells = new();
    readonly List<PuzzleBoardZone> zones = new();
    readonly Dictionary<Vector2Int, PuzzleBoardZone> zoneByCell = new();
    readonly Dictionary<string, PuzzleWagonModification> wagonModifications = new(StringComparer.Ordinal);

    public int Width { get; }
    public int Height { get; }
    internal string DisplayName { get; }
    internal int UsableCellCount => checked(Width * Height - blockedCells.Count);
    internal IReadOnlyCollection<Vector2Int> BlockedCells => blockedCells;
    internal IReadOnlyList<PuzzleBoardZone> Zones => zones;
    // 진행 중인 화차 가공 묶음. 없으면 null이다. 시간 진행 사본과 함께 옮긴다.
    internal PuzzleWagonProcessState ProcessState { get; set; }
    internal int WagonNumber { get; set; }
    internal PuzzleWagonProductData WagonProduct { get; set; }
    internal long WagonSalePrice { get; set; }
    public IReadOnlyCollection<PuzzlePlacement> Placements => placements.Values;
    internal IReadOnlyCollection<PuzzleWagonModification> WagonModifications => wagonModifications.Values;
    internal PuzzleValue Value => valuation.Total;
    internal bool HasUnreadEventNotice { get; set; }

    internal PuzzleInventory(PuzzleBoardData _board, IReadOnlyList<PuzzleStartingPlacementData> _startingPlacements,
        IReadOnlyDictionary<string, PuzzleItemDefinition> _definitions, IReadOnlyList<PuzzleSynergyRuleData> _synergyRules,
        ref int _lastInstanceId)
    {
        Width = _board.width;
        Height = _board.height;
        DisplayName = _board.displayName;
        foreach (var cell in _board.blockedCells ?? Array.Empty<PuzzleBoardCellData>())
        {
            blockedCells.Add(new Vector2Int(cell.x, cell.y));
        }
        foreach (var zone in _board.zones ?? Array.Empty<PuzzleBoardZoneData>())
        {
            AddZone(new PuzzleBoardZone(zone));
        }
        definitions = _definitions;
        synergyRules = _synergyRules;
        foreach (var placement in _startingPlacements)
        {
            var definition = definitions[placement.pieceId];
            var rotation = new PuzzleRotation(definition.Cells, placement.clockwiseTurns);
            var origin = new Vector2Int(placement.x, placement.y);
            if (!TryCreatePose(definition.Cells, origin, rotation, out var pose) ||
                CheckPlacement(pose.Cells, null, definition) != PuzzlePlacementFailure.None)
            {
                throw new InvalidOperationException($"'{placement.id}' 시작 배치의 좌표를 만들 수 없습니다.");
            }

            StorePlacement(new PuzzlePlacement(checked(++_lastInstanceId), definition, this, pose, definition.BaseValue));
        }
        RecalculateValue();
    }

    void AddZone(PuzzleBoardZone _zone)
    {
        zones.Add(_zone);
        foreach (var cell in _zone.Cells)
        {
            zoneByCell[cell] = _zone;
        }
    }

    // 칸이 속한 전용 구역. 구역 밖이면 null이다.
    internal PuzzleBoardZone ZoneAt(Vector2Int _position)
    {
        return zoneByCell.TryGetValue(_position, out var zone) ? zone : null;
    }

    // 포즈의 모든 칸이 그 화물을 받는지. 구역 밖 칸은 제한이 없다. 수용 한계는 배치할 칸 수로 본다.
    bool ZonesAccept(IReadOnlyList<PuzzleCell> _cells, PuzzleItemDefinition _definition)
    {
        foreach (var cell in _cells)
        {
            if (zoneByCell.TryGetValue(cell.Position, out var zone) && !zone.Accepts(_definition, _cells.Count))
            {
                return false;
            }
        }
        return true;
    }

    internal bool TryGetPlacement(int _instanceId, out PuzzlePlacement _placement)
    {
        return placements.TryGetValue(_instanceId, out _placement);
    }

    internal PuzzleValue ValueOf(int _instanceId)
    {
        return valuation.ValueOf(_instanceId);
    }

    internal decimal FreshnessAdjustedValueOf(int _instanceId)
    {
        return placements[_instanceId].FreshnessAdjustedValue;
    }

    // 이 보드에 해당 인스턴스가 아직 있는지. 미리보기 진행에서 소모된 품목의 값을 안전하게 처리할 때 쓴다.
    internal bool HasPlacement(int _instanceId)
    {
        return placements.ContainsKey(_instanceId);
    }

    internal bool TryInstallWagonModification(PuzzleWagonModification _modification)
    {
        return !wagonModifications.ContainsKey(_modification.Id) && _modification.CanInstallOn(this) &&
            wagonModifications.TryAdd(_modification.Id, _modification);
    }

    internal bool HasWagonModification(string _id)
    {
        return wagonModifications.ContainsKey(_id);
    }

    internal PuzzleInventory CopyForTimeAdvance()
    {
        var copy = new PuzzleInventory(this);
        foreach (var placement in placements.Values)
        {
            copy.StorePlacement(copy.CopyPlacement(placement));
        }
        copy.RecalculateValue();
        return copy;
    }

    PuzzleInventory(PuzzleInventory _source)
    {
        Width = _source.Width;
        Height = _source.Height;
        DisplayName = _source.DisplayName;
        blockedCells.UnionWith(_source.blockedCells);
        foreach (var zone in _source.zones)
        {
            AddZone(zone);
        }
        WagonNumber = _source.WagonNumber;
        WagonProduct = _source.WagonProduct;
        WagonSalePrice = _source.WagonSalePrice;
        ProcessState = _source.ProcessState;
        definitions = _source.definitions;
        synergyRules = _source.synergyRules;
        SynergyComboBonusPercent = _source.SynergyComboBonusPercent;
        wagonModifications = new Dictionary<string, PuzzleWagonModification>(_source.wagonModifications, StringComparer.Ordinal);
    }

    internal void CommitTimeAdvance(PuzzleInventory _staged)
    {
        // 계획에서 사라진 입력(조합 소모)은 원본에도 남기지 않는다. 점유는 아래에서 사본대로 다시 채운다.
        foreach (int instanceId in placements.Keys.Where(_id => !_staged.placements.ContainsKey(_id)).ToArray())
        {
            placements.Remove(instanceId);
        }
        occupancy.Clear();
        foreach (var staged in _staged.placements.Values)
        {
            if (placements.TryGetValue(staged.InstanceId, out var existing) && existing.Pose == staged.Pose &&
                existing.Definition == staged.Definition)
            {
                existing.CopyTimeState(staged);
                StorePlacement(existing);
            }
            else
            {
                StorePlacement(CopyPlacement(staged));
            }
        }
        valuation = _staged.valuation;
        ProcessState = _staged.ProcessState;
        HasUnreadEventNotice |= _staged.HasUnreadEventNotice;
    }

    PuzzlePlacement CopyPlacement(PuzzlePlacement _placement)
    {
        return CopyPlacement(_placement, _placement.Definition, this, _placement.Pose, _placement.ActualValue);
    }

    // 정의·소속·좌표만 바꾸고 시간 상태는 옮긴다. 성장 품목이 아닌 정의로는 성장 시계를 넘기지 않는다.
    PuzzlePlacement CopyPlacement(PuzzlePlacement _source, PuzzleItemDefinition _definition, PuzzleInventory _owner,
        PuzzlePose _pose, decimal _actualValue)
    {
        bool keepsGrowth = _definition.GrowthHours > 0 && _source.Definition.GrowthHours > 0;
        // 성장 품목이 성체로 바뀌면 대기가 끝나므로 공간 부족 알림도 넘기지 않는다. 번식 대기는 그대로 옮긴다.
        bool keepsSpaceNotice = _source.Definition.GrowthHours <= 0 || keepsGrowth;
        return new PuzzlePlacement(_source.InstanceId, _definition, _owner, _pose, _actualValue,
            _source.OwnedHours, _source.Durability, _source.ShelfLifeAge)
        {
            HasUnreadSynergySettlement = _source.HasUnreadSynergySettlement,
            HasUnreadSpaceNotice = keepsSpaceNotice && _source.HasUnreadSpaceNotice,
            HasUnreadSpawnNotice = _source.HasUnreadSpawnNotice,
            GrowthElapsedHours = keepsGrowth ? _source.GrowthElapsedHours : (_source.OwnedHours < 0 ? -1 : 0),
            GrowthSpaceBlocked = keepsGrowth && _source.GrowthSpaceBlocked,
            AppliedUpgradeIds = _source.AppliedUpgradeIds,
            SaltUsesRemaining = _source.SaltUsesRemaining,
            BindingId = _source.BindingId,
            BindingLocalOffset = _source.BindingLocalOffset,
            PlantStemCells = _source.PlantStemCells,
            PlantGrowthElapsedHours = _source.PlantGrowthElapsedHours,
            PlantSpaceBlocked = _source.PlantSpaceBlocked
        };
    }

    internal void RecalculateValue()
    {
        valuation.Recalculate(this, synergyRules, SynergyComboBonusPercent);
    }

    // 정산 계획의 작업 사본에 현재 배치의 시너지를 적용한다.
    internal bool ApplySynergyActivations(long _count)
    {
        if (_count == 0)
        {
            return false;
        }
        bool changed = false;
        foreach (var placement in placements.Values)
        {
            var value = valuation.ValueOf(placement.InstanceId);
            decimal before = placement.ActualValue;
            placement.ApplySynergySettlement((value.FinalValue - value.BaseValue) * _count);
            changed |= before != placement.ActualValue;
        }
        if (changed)
        {
            HasUnreadEventNotice = true;
        }
        return changed;
    }

    public bool TryGetCell(Vector2Int _position, out PuzzlePlacement _placement, out PuzzleCell _cell)
    {
        if (occupancy.TryGetValue(_position, out var occupant))
        {
            _placement = placements[occupant.InstanceId];
            _cell = occupant.Cell;
            return true;
        }

        _placement = null;
        _cell = default;
        return false;
    }

    internal bool IsUsableCell(Vector2Int _position) => InBounds(_position);

    // 이 보드가 지금 점유한 칸 수. 노선 이동 비용의 보유량 계산에 쓴다.
    internal int OccupiedCells => occupancy.Count;

    // 식물이 성장할 수 있는 보드 칸인지. 경계·막힘·점유와 전용 구역 규칙을 함께 본다.
    internal bool CanGrowInto(Vector2Int _position, PuzzleItemDefinition _definition, int _cellCount)
    {
        if (!InBounds(_position) || occupancy.ContainsKey(_position))
        {
            return false;
        }
        PuzzleBoardZone zone = ZoneAt(_position);
        return zone == null || zone.Accepts(_definition, _cellCount);
    }

    // 해당 보드 칸을 점유한 배치. 비어 있으면 null이다.
    internal PuzzlePlacement PlacementAt(Vector2Int _position)
    {
        return occupancy.TryGetValue(_position, out var entry) &&
            placements.TryGetValue(entry.InstanceId, out var placement) ? placement : null;
    }

    // 성장 대상을 결정적 순서로 처리한다. 성체 자리가 있으면 시계를 흘려보내고 완성이면 교체하고,
    // 없으면 그 품목의 성장만 대기 지점에서 멈춘다. 다른 화물의 성장·신선도와 게임 진행은 막지 않는다.
    internal void PlanGrowth(List<PuzzleGrowth> _growth)
    {
        var approachingGrowth = placements.Values
            .Where(_item => _item.GrowthElapsedHours >= 0 && _item.Definition.GrowthHours > 0 &&
                (_item.GrowthSpaceBlocked ||
                    _item.GrowthElapsedHours >= _item.Definition.GrowthFreezeHours))
            .OrderBy(_item => _item.Definition.GrowthHours - _item.GrowthElapsedHours)
            .ThenBy(_item => _item.InstanceId);
        HashSet<Vector2Int> occupied = null;
        foreach (var item in approachingGrowth)
        {
            occupied ??= new HashSet<Vector2Int>(occupancy.Keys);
            occupied.ExceptWith(item.Pose.Cells.Select(_cell => _cell.Position));
            var adult = definitions[item.Definition.AdultPieceId];
            if (!TryFindGrowthPose(item.Pose, adult, occupied, out var pose))
            {
                item.WaitForGrowthSpace();
                continue;
            }

            // 미리 확보한 자리는 같은 계획의 다른 송아지가 차지하지 못한다.
            occupied.UnionWith(pose.Cells.Select(_cell => _cell.Position));
            item.ResumeGrowth();
            if (item.GrowthElapsedHours >= item.Definition.GrowthHours)
            {
                _growth.Add(new PuzzleGrowth(item, adult, pose));
            }
        }
    }

    bool TryFindGrowthPose(PuzzlePose _current, PuzzleItemDefinition _adult, HashSet<Vector2Int> _occupied,
        out PuzzlePose _pose)
    {
        var currentRotation = new PuzzleRotation(_adult.Cells, _current.ClockwiseTurns);
        if (TryCreateGrowthPose(_adult.Cells, _current.Origin, currentRotation, _occupied, _adult, out _pose))
        {
            return true;
        }

        for (int turn = 0; turn < PuzzleRotation.TURN_COUNT; turn++)
        {
            var rotation = new PuzzleRotation(_adult.Cells, (_current.ClockwiseTurns + turn) % PuzzleRotation.TURN_COUNT);
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (TryCreateGrowthPose(_adult.Cells, new Vector2Int(x, y), rotation, _occupied, _adult, out _pose))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    bool TryCreateGrowthPose(IReadOnlyList<Vector2Int> _cells, Vector2Int _origin, PuzzleRotation _rotation,
        HashSet<Vector2Int> _occupied, PuzzleItemDefinition _adult, out PuzzlePose _pose)
    {
        return TryCreatePose(_cells, _origin, _rotation, out _pose) &&
            _pose.Cells.All(_cell => InBounds(_cell.Position) && !_occupied.Contains(_cell.Position)) &&
            ZonesAccept(_pose.Cells, _adult);
    }

    internal void ApplyGrowth(PuzzleGrowth _growth)
    {
        var item = _growth.Item;
        var adult = _growth.Adult;
        var grown = new PuzzlePlacement(item.InstanceId, adult, this, _growth.Pose,
            item.ActualValue + (adult.BaseValue - (decimal)item.Definition.BaseValue), item.OwnedHours)
        {
            HasUnreadSynergySettlement = item.HasUnreadSynergySettlement,
            HasUnreadSpawnNotice = item.HasUnreadSpawnNotice
        };
        ClearOccupancy(item);
        StorePlacement(grown);
        RecalculateValue();
    }

    internal PuzzlePlacementResult EvaluateMove(PuzzlePlacement _original, PuzzleInventory _destination,
        Vector2Int _grabbedCell, Vector2Int _targetCell, int _clockwiseTurns)
    {
        if (_original == null || !placements.TryGetValue(_original.InstanceId, out var current) ||
            !ReferenceEquals(current, _original))
        {
            return new PuzzlePlacementResult(PuzzlePlacementFailure.StaleSource);
        }

        bool sameBoard = ReferenceEquals(this, _destination);
        if (_destination == null || !ReferenceEquals(definitions, _destination.definitions) ||
            (!sameBoard && _destination.placements.ContainsKey(_original.InstanceId)))
        {
            return new PuzzlePlacementResult(PuzzlePlacementFailure.InvalidDestination);
        }

        // 이미 배치된 화물은 정의가 아니라 인스턴스의 전체 형태(줄기 포함)로 움직인다.
        var failure = _destination.CreatePoseAtSlot(_original.LocalCells, _grabbedCell, _targetCell, _clockwiseTurns,
            out var candidate);
        if (failure != PuzzlePlacementFailure.None)
        {
            return new PuzzlePlacementResult(failure);
        }

        // 목재 묶음은 잡은 조각 하나가 아니라 묶음 전체가 함께 움직인다. 전 회워가 함께 들어갈 때만 허용한다.
        if (_original.BindingId != 0)
        {
            return new PuzzlePlacementResult(EvaluateBundleMove(_original, _destination, _grabbedCell, _targetCell,
                _clockwiseTurns, out _), candidate);
        }

        int? ignoredInstanceId = sameBoard ? _original.InstanceId : null;
        return new PuzzlePlacementResult(_destination.CheckPlacement(candidate.Cells, ignoredInstanceId, _original.Definition),
            candidate);
    }

    internal PuzzlePlacementResult Move(PuzzlePlacement _original, PuzzleInventory _destination, Vector2Int _grabbedCell,
        Vector2Int _targetCell, int _clockwiseTurns)
    {
        var result = EvaluateMove(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns);
        if (result.CanPlace)
        {
            if (_original.BindingId != 0)
            {
                ApplyBundleMove(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns);
            }
            else
            {
                // 원본과 대상의 현재 점유를 확인한 뒤 같은 호출에서 소속과 점유를 함께 바꾼다.
                var placement = _destination.CopyPlacement(_original, _original.Definition, _destination, result.Candidate,
                    _original.ActualValue);
                ClearOccupancy(_original);
                placements.Remove(_original.InstanceId);
                _destination.StorePlacement(placement);
                RecalculateValue();
                if (!ReferenceEquals(this, _destination))
                {
                    _destination.RecalculateValue();
                }
            }
        }

        return result;
    }

    // 목재 묶음 회원. 미결합 품목은 자기 하나만 담는다. 회원은 같은 BindingId와 같은 소속을 공유한다.
    internal List<PuzzlePlacement> BundleMembers(PuzzlePlacement _placement)
    {
        if (_placement == null)
        {
            return new List<PuzzlePlacement>();
        }
        if (_placement.BindingId == 0)
        {
            return new List<PuzzlePlacement> { _placement };
        }
        return placements.Values
            .Where(_item => _item.BindingId == _placement.BindingId)
            .OrderBy(_item => _item.InstanceId)
            .ToList();
    }

    // 항목 하나의 논리적 가치. 미결합은 신선도를 반영한 가치, 묶음 회원은 묶음 원자(인스턴스 ID 최소)에
    // 모인 가치 합이다. 보드 합계는 회원 가치가 원자에만 남아 중복 없이 같다.
    internal decimal BundleValueOf(int _instanceId)
    {
        if (!placements.TryGetValue(_instanceId, out var placement))
        {
            return 0;
        }
        return placement.BindingId == 0
            ? placement.FreshnessAdjustedValue
            : BundleMembers(placement).Sum(_member => _member.ActualValue);
    }

    // 묶음 dedup과 순서를 안정적으로 만들기 위한 회원 중 가장 작은 인스턴스 ID.
    internal int BundleRootId(PuzzlePlacement _placement)
    {
        if (_placement == null)
        {
            return 0;
        }
        if (_placement.BindingId == 0)
        {
            return _placement.InstanceId;
        }
        int root = _placement.InstanceId;
        foreach (var item in placements.Values)
        {
            if (item.BindingId == _placement.BindingId && item.InstanceId < root)
            {
                root = item.InstanceId;
            }
        }
        return root;
    }

    // 목재 결합 완료. 두 회원을 하나의 새 id로 다시 만들고 인스턴스 ID가 최소인 회원을 원자로 삼는다.
    // 묶음 가치 = ⌈두 묶음 가치 합 × _valuePercent / 100⌉을 원자에 모으고 나머지 회원 가치는 0으로 둔다.
    // 좌표는 움직이지 않는다.
    internal bool TryBind(PuzzlePlacement _left, PuzzlePlacement _right, int _valuePercent, int _bindingId)
    {
        if (_bindingId == 0 || _valuePercent <= 0 || _left == null || _right == null ||
            ReferenceEquals(_left, _right) || !IsCurrentPlacement(_left) || !IsCurrentPlacement(_right) ||
            (_left.BindingId != 0 && _left.BindingId == _right.BindingId))
        {
            return false;
        }

        var members = BundleMembers(_left).Concat(BundleMembers(_right))
            .OrderBy(_member => _member.InstanceId)
            .ToList();
        decimal total = members.Sum(_member => _member.ActualValue);
        decimal merged = (decimal)Math.Ceiling(total * _valuePercent / 100m);
        int anchorId = members[0].InstanceId;
        var anchorPosition = members[0].Pose.Cells[0].Position;
        foreach (var member in members)
        {
            var bound = CopyPlacement(member, member.Definition, this, member.Pose,
                member.InstanceId == anchorId ? merged : 0m);
            bound.BindingId = _bindingId;
            bound.BindingLocalOffset = member.Pose.Cells[0].Position - anchorPosition;
            ClearOccupancy(member);
            placements.Remove(member.InstanceId);
            StorePlacement(bound);
        }
        RecalculateValue();
        return true;
    }

    // 식물 성장 확장 원시 동작. 씨앗·줄기 품목의 보드 후보 칸 하나를 줄기 칸으로 더한다.
    // 보드 경계·막힌 칸·다른 화물 점유·전용 구역을 통과해야만 형태와 점유가 바뀌고, 기존 칸의 보드 위치는
    // 그대로 유지된다. 인스턴스 ID·시간 상태·강화는 보존하고 실제 가치에 _valueDelta를 더한다. 실패하면 보드는 그대로다.
    internal bool TryExtendPlant(PuzzlePlacement _placement, Vector2Int _boardCell, decimal _valueDelta = 0)
    {
        if (!IsCurrentPlacement(_placement) || _placement.Definition.SeedCell == null ||
            !InBounds(_boardCell) ||
            (occupancy.TryGetValue(_boardCell, out var occupant) && occupant.InstanceId != _placement.InstanceId))
        {
            return false;
        }

        var rotation = new PuzzleRotation(_placement.LocalCells, _placement.Pose.ClockwiseTurns);
        Vector2Int local = rotation.GetLocalCell(_boardCell - _placement.Pose.Origin);
        if (_placement.LocalCells.Contains(local))
        {
            return false;
        }

        var shape = new List<Vector2Int>(_placement.LocalCells) { local };
        var grown = new PuzzleRotation(shape, _placement.Pose.ClockwiseTurns);
        Vector2Int anchor = shape[0];
        Vector2Int origin = _placement.Pose.Origin -
            (grown.GetBoardOffset(anchor) - rotation.GetBoardOffset(anchor));
        if (!TryCreatePose(shape, origin, grown, out var pose))
        {
            return false;
        }
        foreach (var cell in pose.Cells)
        {
            if (!InBounds(cell.Position))
            {
                return false;
            }
        }
        if (CheckPlacement(pose.Cells, _placement.InstanceId, _placement.Definition) != PuzzlePlacementFailure.None)
        {
            return false;
        }

        // 기존 칸은 쉰 뒤 새 형태 한 번으로 다시 점유한다. 점유했던 후보 칸은 위 확인으로 비어 있다.
        ClearOccupancy(_placement);
        var grownPlacement = CopyPlacement(_placement, _placement.Definition, this, pose, _placement.ActualValue);
        grownPlacement.PlantStemCells = shape.GetRange(_placement.Definition.Cells.Count,
            shape.Count - _placement.Definition.Cells.Count);
        if (_valueDelta != 0)
        {
            grownPlacement.AddPlantGrowthValue(_valueDelta);
        }
        StorePlacement(grownPlacement);
        RecalculateValue();
        return true;
    }

    // 잡은 회원의 목표 포즈(_anchorPose)와 강체로 함께 움직이는 회원 전원의 포즈. 드래그 고스트 표시용이며
    // 대상 보드는 검사하지 않는다. 수식은 EvaluateBundleMove와 같은 틀을 쓴다.
    internal List<(PuzzlePlacement Member, PuzzlePose Pose)> GetBundleGhostPoses(
        PuzzlePlacement _original, Vector2Int _grabbedCell, PuzzlePose _anchorPose)
    {
        var poses = new List<(PuzzlePlacement Member, PuzzlePose Pose)>();
        if (_original?.Pose == null || _anchorPose == null ||
            !TryGrabbedCellPosition(_original, _grabbedCell, out var grabbedPosition))
        {
            return poses;
        }

        var anchorCell = Vector2Int.zero;
        bool anchored = false;
        foreach (var cell in _anchorPose.Cells)
        {
            if (cell.OriginalCell == _grabbedCell)
            {
                anchorCell = cell.Position;
                anchored = true;
                break;
            }
        }

        if (!anchored)
        {
            return poses;
        }

        int delta = (_anchorPose.ClockwiseTurns - _original.Pose.ClockwiseTurns + PuzzleRotation.TURN_COUNT) %
            PuzzleRotation.TURN_COUNT;
        foreach (var member in BundleMembers(_original))
        {
            var rotation = new PuzzleRotation(member.LocalCells,
                (member.Pose.ClockwiseTurns + delta) % PuzzleRotation.TURN_COUNT);
            var first = member.Pose.Cells[0];
            if (!TryTranslate(anchorCell, RotateBoard(first.Position - grabbedPosition, delta), out var mappedFirst) ||
                !TryTranslate(mappedFirst, -rotation.GetBoardOffset(first.OriginalCell), out var origin) ||
                !TryCreatePose(member.LocalCells, origin, rotation, out var pose))
            {
                continue;
            }
            poses.Add((member, pose));
        }

        return poses;
    }

    // 묶음 회원 전원의 강체 이동 포즈를 만들고 대상 보드 적합성을 함께 본다. 회전은 잡은 칸을 중심으로
    // 전체가 함께 돌며, 포즈는 잡은 조각의 CreatePoseAtSlot 결과와 같은 움직임을 따른다.
    PuzzlePlacementFailure EvaluateBundleMove(PuzzlePlacement _original, PuzzleInventory _destination,
        Vector2Int _grabbedCell, Vector2Int _targetCell, int _clockwiseTurns,
        out List<(PuzzlePlacement Member, PuzzlePose Pose)> _poses)
    {
        _poses = null;
        var members = BundleMembers(_original);
        if (members.Any(_member => !ReferenceEquals(_member.Owner, _original.Owner)) ||
            !TryGrabbedCellPosition(_original, _grabbedCell, out var grabbedPosition))
        {
            return PuzzlePlacementFailure.InvalidDestination;
        }

        int delta = (_clockwiseTurns - _original.Pose.ClockwiseTurns + PuzzleRotation.TURN_COUNT) %
            PuzzleRotation.TURN_COUNT;
        var memberIds = new HashSet<int>(members.Select(_member => _member.InstanceId));
        var poses = new List<(PuzzlePlacement, PuzzlePose)>(members.Count);
        foreach (var member in members)
        {
            var rotation = new PuzzleRotation(member.LocalCells,
                (member.Pose.ClockwiseTurns + delta) % PuzzleRotation.TURN_COUNT);
            var first = member.Pose.Cells[0];
            if (!TryTranslate(_targetCell, RotateBoard(first.Position - grabbedPosition, delta), out var mappedFirst) ||
                !TryTranslate(mappedFirst, -rotation.GetBoardOffset(first.OriginalCell), out var origin) ||
                !TryCreatePose(member.LocalCells, origin, rotation, out var pose))
            {
                return PuzzlePlacementFailure.CellOutsideBoard;
            }
            poses.Add((member, pose));
        }

        foreach (var pose in poses)
        {
            foreach (var cell in pose.Item2.Cells)
            {
                if (!_destination.InBounds(cell.Position))
                {
                    return PuzzlePlacementFailure.CellOutsideBoard;
                }
            }
        }
        foreach (var pose in poses)
        {
            foreach (var cell in pose.Item2.Cells)
            {
                if (_destination.occupancy.TryGetValue(cell.Position, out var occupant) &&
                    !memberIds.Contains(occupant.InstanceId))
                {
                    return PuzzlePlacementFailure.CellOccupied;
                }
            }
        }
        foreach (var (member, pose) in poses)
        {
            if (!_destination.ZonesAccept(pose.Cells, member.Definition))
            {
                return PuzzlePlacementFailure.ZoneRestricted;
            }
        }

        _poses = poses;
        return PuzzlePlacementFailure.None;
    }

    void ApplyBundleMove(PuzzlePlacement _original, PuzzleInventory _destination, Vector2Int _grabbedCell,
        Vector2Int _targetCell, int _clockwiseTurns)
    {
        if (EvaluateBundleMove(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns, out var poses) !=
            PuzzlePlacementFailure.None)
        {
            return;
        }

        // 회원 새 포즈는 다른 회원의 옛 칸과 겹칠 수 있다(회전·이동 합법). 전부 비운 뒤 다시 채운다.
        foreach (var (member, _) in poses)
        {
            member.Owner.ClearOccupancy(member);
            member.Owner.placements.Remove(member.InstanceId);
        }
        foreach (var (member, pose) in poses)
        {
            var moved = _destination.CopyPlacement(member, member.Definition, _destination, pose, member.ActualValue);
            _destination.StorePlacement(moved);
        }
        RecalculateValue();
        if (!ReferenceEquals(this, _destination))
        {
            _destination.RecalculateValue();
        }
    }

    static bool TryGrabbedCellPosition(PuzzlePlacement _placement, Vector2Int _grabbedCell, out Vector2Int _position)
    {
        foreach (var cell in _placement.Pose.Cells)
        {
            if (cell.OriginalCell == _grabbedCell)
            {
                _position = cell.Position;
                return true;
            }
        }

        _position = default;
        return false;
    }

    // 보드 좌표 오프셋을 조각 회전과 같은 방향으로 회전시킨다. local y-다운→보드 y-업 이중 변환을 상쇄한 값이다.
    static Vector2Int RotateBoard(Vector2Int _offset, int _clockwiseTurns)
    {
        return _clockwiseTurns switch
        {
            0 => _offset,
            1 => new Vector2Int(_offset.y, -_offset.x),
            2 => new Vector2Int(-_offset.x, -_offset.y),
            _ => new Vector2Int(-_offset.y, _offset.x)
        };
    }

    internal void ReplaceExpiredFood(PuzzleItemDefinition _waste)
    {
        bool replaced = false;
        foreach (var expired in placements.Values.Where(item => item.RemainingShelfLifeHours == 0).ToArray())
        {
            // 기존 식품이 실제로 점유하던 칸에서 교체한다. 회전된 모양의 원점은 빈 칸일 수 있다.
            // 썩는 일은 그 자리에서 일어나므로 전용 구역 제한은 보지 않는다.
            var target = expired.Pose.Cells[0].Position;
            if (CreatePoseAtSlot(_waste.Cells, _waste.Cells[0], target, 0, out var pose) != PuzzlePlacementFailure.None ||
                CheckPlacement(pose.Cells, expired.InstanceId, _waste) is not (PuzzlePlacementFailure.None or
                    PuzzlePlacementFailure.ZoneRestricted))
            {
                continue;
            }

            ClearOccupancy(expired);
            StorePlacement(new PuzzlePlacement(expired.InstanceId, _waste, this, pose, _waste.BaseValue, 0)
            {
                HasUnreadSpawnNotice = true
            });
            replaced = true;
        }

        if (replaced)
        {
            RecalculateValue();
        }
    }

    static bool SameLocalCells(IReadOnlyList<Vector2Int> _left, IReadOnlyList<Vector2Int> _right)
    {
        if (_left == null || _right == null || _left.Count != _right.Count)
        {
            return false;
        }

        var cells = new HashSet<Vector2Int>(_left);
        foreach (var cell in _right)
        {
            if (!cells.Remove(cell))
            {
                return false;
            }
        }

        return cells.Count == 0;
    }

    // 비어 있는 첫 칸에 조각을 새로 올려 배치한다. 마땅한 빈 칸이 없으면 false.
    internal bool TryAutoPlace(PuzzleItemDefinition _definition, int _instanceId, out PuzzlePlacement _placement)
    {
        if (!TryFindAutoPlacement(_definition, out var pose))
        {
            _placement = null;
            return false;
        }

        _placement = PlaceSpawnedItem(_definition, _instanceId, pose);
        return true;
    }

    internal bool TryAutoPlace(PuzzleItemDefinition _definition, ref int _lastInstanceId, bool _allowRotation = false)
    {
        if (!TryFindAutoPlacement(_definition, out var pose, _allowRotation))
        {
            return false;
        }

        PlaceSpawnedItem(_definition, checked(++_lastInstanceId), pose);
        return true;
    }

    // 산출물 _count개를 전부 놓을 수 있을 때만 배치한다. 실패하면 보드는 하나도 바뀌지 않는다.
    internal bool TryAutoPlaceAll(PuzzleItemDefinition _definition, int _count, ref int _lastInstanceId,
        bool _allowRotation = false, bool _markUnread = false)
    {
        if (!TryPlanAutoPlacements(_definition, _count, _allowRotation, out var poses))
        {
            return false;
        }

        foreach (var pose in poses)
        {
            var placed = PlaceSpawnedItem(_definition, checked(++_lastInstanceId), pose);
            placed.HasUnreadSpawnNotice = _markUnread;
        }

        if (_markUnread)
        {
            HasUnreadEventNotice = true;
        }
        return true;
    }

    // 산출물을 여러 개 놓아야 할 때 전부 들어가는지 상태 없이 확인한다. 부분 생성을 막는다.
    internal bool CanAutoPlaceAll(PuzzleItemDefinition _definition, int _count, bool _allowRotation = false)
    {
        return TryPlanAutoPlacements(_definition, _count, _allowRotation, out _);
    }

    PuzzlePlacement PlaceSpawnedItem(PuzzleItemDefinition _definition, int _instanceId, PuzzlePose _pose)
    {
        var placement = new PuzzlePlacement(_instanceId, _definition, this, _pose, _definition.BaseValue, 0);
        StorePlacement(placement);
        RecalculateValue();
        return placement;
    }

    // 그 화물을 받는 전용 구역 안 자리를 먼저 찾고, 없으면 나머지 자리에서 찾는다(배설물은 똥 전용칸부터).
    bool TryFindAutoPlacement(PuzzleItemDefinition _definition, out PuzzlePose _pose, bool _allowRotation = false,
        ICollection<Vector2Int> _claimed = null)
    {
        for (int pass = zones.Count > 0 ? 0 : 1; pass < 2; pass++)
        {
            bool zoneOnly = pass == 0;
            int turns = _allowRotation ? PuzzleRotation.TURN_COUNT : 1;
            for (int turn = 0; turn < turns; turn++)
            {
                var rotation = new PuzzleRotation(_definition.Cells, turn);
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        if (TryCreatePose(_definition.Cells, new Vector2Int(x, y), rotation, out var pose) &&
                            CheckPlacement(pose.Cells, null, _definition) == PuzzlePlacementFailure.None &&
                            (!zoneOnly || pose.Cells.All(_cell => zoneByCell.ContainsKey(_cell.Position))) &&
                            (_claimed == null || pose.Cells.All(_cell => !_claimed.Contains(_cell.Position))))
                        {
                            _pose = pose;
                            return true;
                        }
                    }
                }
            }
        }

        _pose = null;
        return false;
    }

    // 같은 조각을 _count개 놓을 포즈를 상태 변경 없이 정한다. 하나라도 못 들어가면 false.
    bool TryPlanAutoPlacements(PuzzleItemDefinition _definition, int _count, bool _allowRotation,
        out List<PuzzlePose> _poses)
    {
        _poses = _count > 0 ? new List<PuzzlePose>(_count) : null;
        if (_count <= 0)
        {
            return false;
        }

        var claimed = new HashSet<Vector2Int>();
        for (int i = 0; i < _count; i++)
        {
            if (!TryFindAutoPlacement(_definition, out var pose, _allowRotation, claimed))
            {
                _poses = null;
                return false;
            }
            _poses.Add(pose);
            foreach (var cell in pose.Cells)
            {
                claimed.Add(cell.Position);
            }
        }

        return true;
    }

    // 화차 가공 완료. 입력을 모두 없애고 산출물 _count개를 놓는다. _fixedCells가 있으면 그 칸에 하나씩(1칸 산출물),
    // 없으면 자동 배치(전용 구역 우선)로 놓는다. 전부 놓을 수 없으면 보드는 하나도 바뀌지 않는다.
    internal bool TryReplaceWithOutputs(IReadOnlyList<PuzzlePlacement> _inputs, PuzzleItemDefinition _output, int _count,
        IReadOnlyList<Vector2Int> _fixedCells, ref int _lastInstanceId)
    {
        if (_inputs == null || _inputs.Count == 0 || _output == null || _count <= 0 ||
            _inputs.Any(_input => _input == null || !IsCurrentPlacement(_input)))
        {
            return false;
        }

        foreach (var input in _inputs)
        {
            ClearOccupancy(input);
        }
        List<PuzzlePose> poses;
        bool planned;
        if (_fixedCells != null)
        {
            planned = _fixedCells.Count == _count && _output.Cells.Count == 1;
            poses = new List<PuzzlePose>(_count);
            for (int i = 0; planned && i < _fixedCells.Count; i++)
            {
                planned = CreatePoseAtSlot(_output.Cells, _output.Cells[0], _fixedCells[i], 0, out var pose) ==
                    PuzzlePlacementFailure.None && CheckPlacement(pose.Cells, null, _output) == PuzzlePlacementFailure.None;
                poses.Add(pose);
            }
        }
        else
        {
            planned = TryPlanAutoPlacements(_output, _count, false, out poses);
        }

        if (!planned)
        {
            foreach (var input in _inputs)
            {
                StorePlacement(input);
            }
            return false;
        }

        foreach (var input in _inputs)
        {
            placements.Remove(input.InstanceId);
        }
        foreach (var pose in poses)
        {
            var placed = PlaceSpawnedItem(_output, checked(++_lastInstanceId), pose);
            placed.HasUnreadSpawnNotice = true;
        }
        HasUnreadEventNotice = true;
        return true;
    }

    // 이 보드에서 배치 하나를 완전히 제거한다. 도축처럼 소멸시키는 행동만 사용하고, 반환값은 소속이 맞는 경우에만 true다.
    internal bool Remove(PuzzlePlacement _placement)
    {
        if (_placement == null || !placements.TryGetValue(_placement.InstanceId, out var current) ||
            !ReferenceEquals(current, _placement))
        {
            return false;
        }

        ClearOccupancy(_placement);
        placements.Remove(_placement.InstanceId);
        RecalculateValue();
        return true;
    }

    // 조합 완료 판정. 두 입력이 이 보드의 현재 인스턴스이고 상하좌우로 붙어 있으며, 결과를 자리를
    // 이어받는 입력의 칸에 그대로 놓을 수 있을 때만 참이다. 상태는 바꾸지 않는다.
    internal bool CanCombine(PuzzlePlacement _source, PuzzlePlacement _partner, PuzzleItemDefinition _result)
    {
        if (_source == null || _partner == null || _result == null || ReferenceEquals(_source, _partner) ||
            !ReferenceEquals(_source.Owner, this) || !ReferenceEquals(_partner.Owner, this) ||
            !IsCurrentPlacement(_source) || !IsCurrentPlacement(_partner) ||
            !SameLocalCells(_source.Definition.Cells, _result.Cells) || !ZonesAccept(_source.Pose.Cells, _result) ||
            !PuzzleRefrigeration.TryGetContact(_source, _partner, out _))
        {
            return false;
        }

        // 결과가 앉을 칸은 이번 조합으로 비는 두 입력의 칸이어야 한다. 다른 물건이 끼어 있으면 성립하지 않는다.
        foreach (var cell in _source.Pose.Cells)
        {
            if (!occupancy.TryGetValue(cell.Position, out var occupant) ||
                (occupant.InstanceId != _source.InstanceId && occupant.InstanceId != _partner.InstanceId))
            {
                return false;
            }
        }

        return true;
    }

    // 두 입력을 소모해 결과 하나를 만든다. 결과는 자리를 이어받는 입력의 자리·회전을 이어받고 새 인스턴스로
    // 태어나므로 보유 시간과 신선도 나이는 0부터 시작한다. 실패하면 입력도 보드도 그대로다.
    internal bool TryCombine(PuzzlePlacement _source, PuzzlePlacement _partner, PuzzleItemDefinition _result,
        ref int _lastInstanceId)
    {
        if (!CanCombine(_source, _partner, _result))
        {
            return false;
        }

        PuzzlePose pose = _source.Pose;
        ClearOccupancy(_source);
        ClearOccupancy(_partner);
        placements.Remove(_source.InstanceId);
        placements.Remove(_partner.InstanceId);
        PlaceSpawnedItem(_result, checked(++_lastInstanceId), pose);
        return true;
    }

    // 절임 완료. 음식만 소모해 결과를 자리에 이어받고 소금은 남긴 채 사용 횟수를 하나 깎는다.
    // 마지막 횟수를 쓰면 소금도 사라진다. 횟수가 없거나 입력이 바뀌었으면 보드는 그대로다.
    internal bool TryPickle(PuzzlePlacement _food, PuzzlePlacement _salt, PuzzleItemDefinition _result,
        PuzzleSaltUseData _saltData, ref int _lastInstanceId)
    {
        if (!IsCurrentPlacement(_salt) || !ReferenceEquals(_salt.Owner, this) ||
            _salt.SaltUsesLeft(_saltData.usesPerItem) <= 0 || !CanCombine(_food, _salt, _result))
        {
            return false;
        }

        int remaining = _salt.SaltUsesLeft(_saltData.usesPerItem);
        PuzzlePose pose = _food.Pose;
        ClearOccupancy(_food);
        placements.Remove(_food.InstanceId);
        PlaceSpawnedItem(_result, checked(++_lastInstanceId), pose);
        if (remaining > 1)
        {
            _salt.SaltUsesRemaining = remaining - 1;
        }
        else
        {
            Remove(_salt);
        }
        return true;
    }

    bool IsCurrentPlacement(PuzzlePlacement _placement)
    {
        return placements.TryGetValue(_placement.InstanceId, out var current) && ReferenceEquals(current, _placement);
    }

    internal PuzzlePose GetDragPose(IReadOnlyList<Vector2Int> _shape, Vector2Int _grabbedCell, int _clockwiseTurns)
    {
        // 보드 밖의 고스트는 잡은 슬롯을 (0, 0)에 둔 포즈에서 상대 위치를 읽는다.
        CreatePoseAtSlot(_shape, _grabbedCell, Vector2Int.zero, _clockwiseTurns, out var pose);
        return pose;
    }

    PuzzlePlacementFailure CreatePoseAtSlot(IReadOnlyList<Vector2Int> _shape, Vector2Int _grabbedCell, Vector2Int _targetCell,
        int _clockwiseTurns, out PuzzlePose _pose)
    {
        _pose = null;
        if (_clockwiseTurns < 0 || _clockwiseTurns >= PuzzleRotation.TURN_COUNT)
        {
            return PuzzlePlacementFailure.InvalidRotation;
        }
        if (!_shape.Contains(_grabbedCell))
        {
            return PuzzlePlacementFailure.InvalidGrabbedCell;
        }

        var rotation = new PuzzleRotation(_shape, _clockwiseTurns);
        var grabbedOffset = rotation.GetBoardOffset(_grabbedCell);
        if (!InBounds(_targetCell) || !TryTranslate(_targetCell, -grabbedOffset, out var origin) ||
            !TryCreatePose(_shape, origin, rotation, out _pose))
        {
            return PuzzlePlacementFailure.CellOutsideBoard;
        }

        return PuzzlePlacementFailure.None;
    }

    static bool TryCreatePose(IReadOnlyList<Vector2Int> _shape, Vector2Int _origin,
        PuzzleRotation _rotation, out PuzzlePose _pose)
    {
        _pose = null;
        var cells = new PuzzleCell[_shape.Count];
        for (int i = 0; i < _shape.Count; i++)
        {
            var original = _shape[i];
            if (!TryTranslate(_origin, _rotation.GetBoardOffset(original), out var position))
            {
                return false;
            }

            cells[i] = new PuzzleCell(original, position);
        }

        _pose = new PuzzlePose(_origin, _rotation.ClockwiseTurns, cells);
        return true;
    }

    PuzzlePlacementFailure CheckPlacement(IReadOnlyList<PuzzleCell> _cells, int? _ignoredInstanceId,
        PuzzleItemDefinition _definition)
    {
        foreach (var cell in _cells)
        {
            if (!InBounds(cell.Position))
            {
                return PuzzlePlacementFailure.CellOutsideBoard;
            }
        }

        foreach (var cell in _cells)
        {
            if (occupancy.TryGetValue(cell.Position, out var occupant) && occupant.InstanceId != _ignoredInstanceId)
            {
                return PuzzlePlacementFailure.CellOccupied;
            }
        }

        return ZonesAccept(_cells, _definition) ? PuzzlePlacementFailure.None : PuzzlePlacementFailure.ZoneRestricted;
    }

    void StorePlacement(PuzzlePlacement _placement)
    {
        placements[_placement.InstanceId] = _placement;
        foreach (var cell in _placement.Pose.Cells)
        {
            occupancy.Add(cell.Position, (_placement.InstanceId, cell));
        }
    }

    void ClearOccupancy(PuzzlePlacement _placement)
    {
        foreach (var cell in _placement.Pose.Cells)
        {
            occupancy.Remove(cell.Position);
        }
    }

    bool InBounds(Vector2Int _position)
    {
        return _position.x >= 0 && _position.x < Width && _position.y >= 0 && _position.y < Height &&
            !blockedCells.Contains(_position);
    }

    static bool TryTranslate(Vector2Int _origin, Vector2Int _offset, out Vector2Int _position)
    {
        long x = (long)_origin.x + _offset.x;
        long y = (long)_origin.y + _offset.y;
        if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
        {
            _position = default;
            return false;
        }

        _position = new Vector2Int((int)x, (int)y);
        return true;
    }
}

internal readonly struct PuzzleGrowth
{
    internal PuzzlePlacement Item { get; }
    internal PuzzleItemDefinition Adult { get; }
    internal PuzzlePose Pose { get; }

    internal PuzzleGrowth(PuzzlePlacement _item, PuzzleItemDefinition _adult, PuzzlePose _pose)
    {
        Item = _item;
        Adult = _adult;
        Pose = _pose;
    }
}

// 보드 전용 구역의 실행 형태. 테이블 행에서 한 번 만들고 바뀌지 않으므로 시간 진행 사본이 같은 인스턴스를 공유한다.
internal sealed class PuzzleBoardZone
{
    readonly HashSet<string> allowedTags;
    internal string Id { get; }
    internal string DisplayName { get; }
    internal IReadOnlyList<string> AllowedTags { get; }
    internal int MaxPieceCells { get; }
    internal IReadOnlyList<Vector2Int> Cells { get; }

    internal PuzzleBoardZone(PuzzleBoardZoneData _data)
    {
        Id = _data.id;
        DisplayName = _data.displayName;
        AllowedTags = Array.AsReadOnly((_data.allowedTags ?? Array.Empty<string>()).ToArray());
        allowedTags = new HashSet<string>(AllowedTags, StringComparer.Ordinal);
        MaxPieceCells = _data.maxPieceCells;
        var cells = new List<Vector2Int>();
        for (int y = _data.y; y < _data.y + _data.height; y++)
        {
            for (int x = _data.x; x < _data.x + _data.width; x++)
            {
                cells.Add(new Vector2Int(x, y));
            }
        }
        Cells = cells.AsReadOnly();
    }

    internal bool Accepts(PuzzleItemDefinition _definition, int _cellCount)
    {
        return _definition != null && _definition.Tags.Any(allowedTags.Contains) &&
            (MaxPieceCells <= 0 || _cellCount <= MaxPieceCells);
    }
}

// 진행 중인 화차 가공 묶음. 재료는 인스턴스 ID와 품목 ID로 기억해 빠지거나 바뀌면 묶음을 버린다.
internal sealed class PuzzleWagonProcessState
{
    internal IReadOnlyList<(int InstanceId, string PieceId)> Inputs { get; }
    internal int Units { get; }
    internal long RequiredHours { get; }
    internal long ElapsedHours { get; }
    // 시간이 다 됐지만 산출물 자리가 없어 기다리는 중.
    internal bool WaitingForSpace { get; }

    internal PuzzleWagonProcessState(IReadOnlyList<(int InstanceId, string PieceId)> _inputs, int _units,
        long _requiredHours, long _elapsedHours, bool _waitingForSpace)
    {
        Inputs = _inputs;
        Units = _units;
        RequiredHours = _requiredHours;
        ElapsedHours = _elapsedHours;
        WaitingForSpace = _waitingForSpace;
    }

    internal PuzzleWagonProcessState With(long _elapsedHours, bool _waitingForSpace)
    {
        return new PuzzleWagonProcessState(Inputs, Units, RequiredHours, _elapsedHours, _waitingForSpace);
    }
}
