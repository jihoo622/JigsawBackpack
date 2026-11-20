using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 개편 레이아웃 창들의 보드에 포인터 이벤트 전달 컴포넌트를 미리 붙인다(런타임 생성 금지 규칙).
// 창 보드는 입력 담당의 자식이 아니라서, 보드 이미지가 이벤트를 받아 입력 담당에 넘겨야 클릭·끌기·툴팁이 동작한다.
// 여러 번 실행해도 결과가 같다.
public static class RelayoutInputBuilder
{
    const string RELAYOUT_PREFAB_FOLDER = "Assets/Art/UI/Relayout";
    const string GAME_CANVAS_PREFAB = "Assets/Art/UI/Scene/GameCanvas.prefab";

    [MenuItem("Tools/MyJG/UI/Relayout/Wire Board Input Forwarders")]
    static void Menu()
    {
        Debug.Log(BuildAll());
    }

    public static string BuildAll()
    {
        var report = new StringBuilder();
        var paths = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { RELAYOUT_PREFAB_FOLDER }))
        {
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }
        paths.Add(GAME_CANVAS_PREFAB);

        foreach (var path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            int wired = 0;
            try
            {
                foreach (var view in root.GetComponentsInChildren<PuzzleInventoryView>(true))
                {
                    // 개편 레이아웃 창 안의 보드만 다룬다. 옛 레이아웃 보드는 이미 입력 담당의 자식이다.
                    if (!view.GetComponentInParent<FloatingWindow>(true))
                    {
                        continue;
                    }
                    var so = new SerializedObject(view);
                    var board = so.FindProperty("board").objectReferenceValue as Image;
                    var viewport = so.FindProperty("boardViewport").objectReferenceValue as RectTransform;
                    // 개편 창의 칸은 보드 안쪽 영역(BoardViewport)에 그려지고 보드 바탕 이미지는 꺼져 있다.
                    // 이벤트를 받을 곳은 칸이 실제로 놓인 안쪽 영역이다.
                    GameObject target = viewport ? viewport.gameObject : board ? board.gameObject : null;
                    if (!target)
                    {
                        continue;
                    }
                    if (board && board.gameObject != target)
                    {
                        var stale = board.GetComponent<InventoryPointerForwarder>();
                        if (stale)
                        {
                            Object.DestroyImmediate(stale, true);
                        }
                    }
                    var hitImage = target.GetComponent<Image>();
                    if (!hitImage)
                    {
                        hitImage = target.AddComponent<Image>();
                        hitImage.color = Color.clear;
                    }
                    hitImage.enabled = true;
                    hitImage.raycastTarget = true;
                    if (!target.GetComponent<InventoryPointerForwarder>())
                    {
                        target.AddComponent<InventoryPointerForwarder>();
                    }
                    EditorUtility.SetDirty(target);
                    wired++;
                }
                if (wired > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            report.AppendLine(path + ": " + wired);
        }
        return report.ToString();
    }
}
