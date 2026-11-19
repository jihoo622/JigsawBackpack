using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class WagonShopDebugWindow : EditorWindow
{
    Vector2 scroll;
    bool showGeneralSpecials;

    [MenuItem("Tools/MyJG/Wagon Shop Debug")]
    public static void Open()
    {
        var window = GetWindow<WagonShopDebugWindow>("화차 판매처 디버그");
        window.minSize = new Vector2(480, 380);
    }

    void OnInspectorUpdate() => Repaint();

    void OnGUI()
    {
        EditorGUILayout.LabelField("현재 회차의 특수 화차 판매처", EditorStyles.boldLabel);
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드에서 게임을 시작하면 실제 배정 결과가 표시됩니다.", MessageType.Info);
            return;
        }

        var session = FindFirstObjectByType<PuzzleInventoryService>();
        if (session == null || session.Train == null)
        {
            EditorGUILayout.HelpBox("게임 세션 초기화를 기다리는 중입니다.", MessageType.Info);
            return;
        }

        var tables = TableManager.Instance;
        var products = tables.PuzzleTrade.wagonProducts;
        EditorGUILayout.HelpBox("회차 시작에 배정된 실제 판매처입니다. 모든 마을은 일반 화차에 특수 화차를 추가로 진열합니다.", MessageType.Info);
        if (GUILayout.Button("지역 특수 화차 배정표 복사"))
        {
            var text = new StringBuilder("화차\t지역\t판매 마을\n");
            foreach (var product in products.Where(_product => !string.IsNullOrEmpty(_product.saleRegionId)))
            {
                text.Append(product.displayName).Append('\t')
                    .Append(RegionName(tables, product.saleRegionId)).Append('\t')
                    .AppendLine(SaleCities(session, tables, product.id));
            }
            EditorGUIUtility.systemCopyBuffer = text.ToString();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var product in products.Where(_product => !string.IsNullOrEmpty(_product.saleRegionId)))
        {
            DrawProduct(session, tables, product, RegionName(tables, product.saleRegionId));
        }
        showGeneralSpecials = EditorGUILayout.Foldout(showGeneralSpecials, "일반 특수 화차 판매처도 보기", true);
        if (showGeneralSpecials)
        {
            foreach (var product in products.Where(_product => _product.randomSale))
            {
                DrawProduct(session, tables, product, "모든 지역");
            }
        }
        EditorGUILayout.EndScrollView();
    }

    static void DrawProduct(PuzzleInventoryService _session, TableManager _tables,
        PuzzleWagonProductData _product, string _region)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"{_product.displayName} ({_product.id})", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("판매 지역", _region);
        EditorGUILayout.LabelField("판매 마을", SaleCities(_session, _tables, _product.id), EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndVertical();
    }

    static string SaleCities(PuzzleInventoryService _session, TableManager _tables, string _productId)
    {
        var ids = _session.GetWagonSaleCityIdsForDebug(_productId);
        return ids.Length == 0 ? "배정 없음" : string.Join(", ",
            ids.Select(_id => $"{_tables.Cities.First(_city => _city.id == _id).displayName} ({_id})"));
    }

    static string RegionName(TableManager _tables, string _regionId) =>
        _tables.Puzzle.regions.First(_region => _region.id == _regionId).displayName;
}
