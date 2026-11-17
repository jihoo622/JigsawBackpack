using System;
using System.Collections.Generic;

// 작업장 공통 계산. 역은 PuzzleCityData.workshopIds로 작업장을 가지고, 작업장은 WorksData(TrainventoryWorkTableData)의
// 작업으로 화물을 처리한다. 수수료와 작업 시간은 맡긴 화물마다 작업장 계수를 거쳐 정한다.
public sealed partial class PuzzleInventoryService
{
    // 맡긴 화물 전체의 수수료 합. 작업이 없는 화물은 확정 단계에서 막히므로 합에서 뺀다.
    long WorkshopFeeOf(TrainventoryWorkshopData _workshop, IEnumerable<PuzzlePlacement> _items)
    {
        long fee = 0;
        foreach (var item in _items)
        {
            if (tables.TryGetWorkFor(_workshop, item.Definition.Id, out var work))
            {
                fee = checked(fee + TableManager.WorkFee(_workshop, work));
            }
        }
        return fee;
    }

    // 함께 맡긴 화물은 동시에 시작해 가장 긴 작업 시간이 지나면 모두 나온다.
    long WorkshopHoursOf(TrainventoryWorkshopData _workshop, IEnumerable<PuzzlePlacement> _items)
    {
        long hours = 0;
        foreach (var item in _items)
        {
            if (tables.TryGetWorkFor(_workshop, item.Definition.Id, out var work))
            {
                hours = Math.Max(hours, work.workHours);
            }
        }
        return hours;
    }
}
