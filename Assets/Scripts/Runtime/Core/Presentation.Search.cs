// 지도 검색 및 전역 검색 UI 문구 단일 출처.
// 기초데이터의 presentation.search.* 키를 참조한다.

public static partial class Presentation
{
    public static string SearchPlaceholder => BasicData.Text("presentation.search.placeholder");
    public static string SearchKindStation => BasicData.Text("presentation.search.kindStation");
    public static string SearchKindRegion => BasicData.Text("presentation.search.kindRegion");
    public static string SearchRouteButton => BasicData.Text("presentation.search.routeButton");
    public static string SearchNoResults => BasicData.Text("presentation.search.noResults");

    public static string SearchRemainingCount(int _count)
    {
        return BasicData.Format("presentation.search.remainingCount", _count);
    }

    public static string SearchKind(EntityLinkKind _kind)
    {
        return _kind == EntityLinkKind.City ? SearchKindStation : SearchKindRegion;
    }
}
