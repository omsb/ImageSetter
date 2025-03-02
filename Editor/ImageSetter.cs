#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine.U2D;
using UnityEditor.U2D;

namespace OMSB
{
    /// <summary>
    /// uGUIのImageの設定ツール
    /// </summary>
    public class ImageSetter : EditorWindow
    {
        //=======================================================================================================
        //. 定数
        //=======================================================================================================
        #region -- 定数

        private const string    TOOL_NAME           = "ImageSetter";
        private const int       PREV_VALUE_DEFAULT  = 40;

        #endregion

        //=======================================================================================================
        //. メンバ
        //=======================================================================================================
        #region -- フィールド

        /// <summary>
        /// 設定するSpriteAtlas
        /// </summary>
        private SpriteAtlas     m_TargetAtlas;

        /// <summary>
        /// SpriteAtlas内のスプライト一覧
        /// </summary>  
        private List<Sprite>    m_SetSprites            = new List<Sprite>();

        /// <summary>
        /// 置き換えウィンドウのスクロールの位置の保持
        /// </summary>
        private Vector2         m_ScrollPos             = Vector2.zero;

        /// <summary>
        /// 検索する名前
        /// </summary>
        private string          m_FindName              = string.Empty;

        /// <summary>
        /// ページ内表示数を変更する？
        /// </summary>
        private bool            m_IsChangePrevValue     = false;

        /// <summary>
        /// ページ内表示数
        /// </summary>
        private int             m_PrevValue             = PREV_VALUE_DEFAULT;

        /// <summary>
        /// 現在のページ数
        /// </summary>
        private int             m_CurrentPage           = 1;

        /// <summary>
        /// ページの最大数
        /// </summary>
        private int             m_MaxPage;

        #endregion

        //=======================================================================================================
        //. UI
        //=======================================================================================================
        #region -- UI

        /// <summary>
        /// メニューのWindowに追加
        /// </summary>
        [MenuItem("OMSB/" + TOOL_NAME)]
        public static void OpenWindow() {
            EditorWindow.GetWindow<ImageSetter>(TOOL_NAME);
        }

        /// <summary>
        /// メイン描画
        /// </summary>
        private void OnGUI() {
            if (EditorGUIEx.DrawGroup("Options")) {
                EditorGUILayout.HelpBox("SpriteAtlas", MessageType.None);

                using (new EditorGUILayout.HorizontalScope()) {
                    m_TargetAtlas = EditorGUILayout.ObjectField("", m_TargetAtlas, typeof(SpriteAtlas), false) as SpriteAtlas;

                    // Atlasが設定されていなかったら更新させない
                    EditorGUI.BeginDisabledGroup(m_TargetAtlas == null);
                    {
                        if (GUILayout.Button("Update", GUILayout.Width(60f))) {
                            GUI.changed = true;
                        }
                    }
                    EditorGUI.EndDisabledGroup();

                    if (GUI.changed) {
                        UpdateSprites();
                    }
                }

                EditorGUILayout.Space();
            }

            if (EditorGUIEx.DrawGroup("Sprite List")) {
                // 対象Atlasが設定されていたら
                if (m_TargetAtlas != null) {
                    // 検索
                    using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                        EditorGUILayout.HelpBox("Filter", MessageType.None);
                        m_FindName = EditorGUILayout.TextField(m_FindName);

                        if (GUI.changed) {
                            UpdateSprites();
                        }
                    }

                    // ページ内表示数 -> 大量の画像を並べると表示がちらつくため40個程度の表示に絞る
                    using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                        m_IsChangePrevValue = EditorGUILayout.ToggleLeft("Page Max Size", m_IsChangePrevValue);
                        using (new EditorGUI.DisabledGroupScope(!m_IsChangePrevValue)) {
                            m_PrevValue = EditorGUILayout.IntSlider(m_PrevValue, 1, m_SetSprites.Count);
                        }
                    }

                    // ページ表示
                    using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                        if (m_SetSprites.Count > 0) {
                            m_MaxPage = Mathf.CeilToInt((float)m_SetSprites.Count / (float)m_PrevValue);
                        }

                        EditorGUILayout.HelpBox($"page : {m_CurrentPage} / {m_MaxPage} , sprites : {m_SetSprites.Count}", MessageType.None);

                        using (new EditorGUI.DisabledGroupScope(m_CurrentPage == 1)) {
                            if (GUILayout.Button("◀", GUILayout.Width(20), GUILayout.Height(20))) {
                                m_CurrentPage--;
                            }
                        }

                        using (new EditorGUI.DisabledGroupScope(m_CurrentPage == m_MaxPage)) {
                            if (GUILayout.Button("▶", GUILayout.Width(20), GUILayout.Height(20))) {
                                m_CurrentPage++;
                            }
                        }
                    }

                    using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ScrollPos, GUI.skin.box))
                    {
                        m_ScrollPos = scrollView.scrollPosition;
                        OnGUI_SetSprites();
                    }
                } else {
                    EditorGUILayout.HelpBox("Please set the SpriteAtlas.", MessageType.Info);
                }
            }
        }

        /// <summary>
        /// 対象のSpriteリストを列挙
        /// </summary>
        private void OnGUI_SetSprites() {
            // リストが空だったら
            if (m_SetSprites.Count <= 0) {
                EditorGUILayout.HelpBox("Sprite None.", MessageType.Info);
                return;
            }

            // 開始,終了箇所を計算
            int startCount = (m_CurrentPage - 1) * m_PrevValue;
            int endCount = startCount + m_PrevValue;
            if (endCount > m_SetSprites.Count) {
                endCount = m_SetSprites.Count;
            }

            // リストを表示
            for (int i = startCount; i < endCount; i++) {
                using (new EditorGUILayout.HorizontalScope(GUI.skin.box)) {
                    if (m_SetSprites[i] == null)
                        continue;

                    // Spriteを編集させない
                    Sprite spriteField;
                    using (new EditorGUI.DisabledGroupScope(true)) {
                        spriteField = EditorGUILayout.ObjectField ("", m_SetSprites[i], typeof(Sprite), false, GUILayout.Width(50), GUILayout.Height(50)) as Sprite;
                    }

                    using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                        EditorGUILayout.HelpBox(m_SetSprites[i]?.name, MessageType.None);
                        if (GUILayout.Button("Set")) {
                            var selectObj = Selection.activeGameObject;
                            if (selectObj == null)
                                continue;

                            var image = selectObj.GetComponent<Image>();

                            // Imageが付いてなかったら付ける
                            if (image == null) {
                                image = Undo.AddComponent<Image>(selectObj);
                            }

                            Undo.RecordObject(image, TOOL_NAME + " - Set Sprite");
                            image.sprite = spriteField;

                            // 元画像にSliceの設定があったらSliceに変更
                            if (CheckSliceSprite(spriteField)) {
                                image.type = Image.Type.Sliced;
                            } else {
                                image.type = Image.Type.Simple;
                            }

                            // Sceneの再描画のためオブジェクトをOn/Off
                            if (image.gameObject.activeSelf) {
                                image.gameObject.SetActive(false);
                                image.gameObject.SetActive(true);
                            }
                        }
                    }
                }
            }
        }

        #endregion
        
        //=======================================================================================================
        //. 取得
        //=======================================================================================================
        #region -- 取得

        /// <summary>
        /// SpriteAtlasに含まれるSpriteリストを取得
        /// </summary>
        /// <param name="_atlas">対象SpriteAtlas</param>
        /// <returns>SpriteAtlasに含まれるSpriteリスト</returns>
        private List<Sprite> GetAtlasSprites(SpriteAtlas _atlas) {
            List<Sprite> sprites = new List<Sprite>();

            if (_atlas == null)
                return sprites;

            // パックされているオブジェクトを取得
            var packables = SpriteAtlasExtensions.GetPackables(_atlas);
            foreach (var p in packables) {
                var path = AssetDatabase.GetAssetPath(p);

                // フォルダ内の画像を一括で取得
                var folderPath = Application.dataPath + path.Replace("Assets", "");
                var dir = new DirectoryInfo(folderPath);
                if (dir == null)
                    continue;
                var fileInfo = dir.GetFiles("*.png");

                foreach (var file in fileInfo) {
                    var sprite = AssetDatabase.LoadAssetAtPath(path + "/" + file.Name, typeof(Sprite)) as Sprite;
                    if (sprite == null)
                        continue;

                    sprites.Add(sprite);
                }
            }

            return sprites;
        }

        /// <summary>
        /// フィルタ名を含むSpriteリストを取得
        /// </summary>
        /// <param name="_name">フィルタ名</param>
        /// <returns>フィルタ名を含むSpriteリスト</returns>
        private List<Sprite> FilterNameSprites(string _name) {
            List<Sprite> sprites = new List<Sprite>();

            var atlasSprites = GetAtlasSprites(m_TargetAtlas);
            foreach (var sprite in atlasSprites) {
                if (sprite == null)
                    continue;

                // 指定文字列が含まれているか
                if (sprite.name.Contains(_name)) {
                    sprites.Add(sprite);
                }
            }

            return sprites;
        }

        #endregion

        //=======================================================================================================
        //. 設定
        //=======================================================================================================
        #region -- 設定

        /// <summary>
        /// 対象Spriteを更新
        /// </summary>
        private void UpdateSprites() {
            m_SetSprites.Clear();

            if (string.IsNullOrEmpty(m_FindName)) {
                m_SetSprites = GetAtlasSprites(m_TargetAtlas);
            } else {
                m_SetSprites = FilterNameSprites(m_FindName);
            }

            // ページ設定
            m_CurrentPage = 1;
            m_MaxPage = 1;
            m_PrevValue = PREV_VALUE_DEFAULT;
            if (m_PrevValue > m_SetSprites.Count) {
                m_PrevValue = m_SetSprites.Count;
            }
        }

        #endregion

        //=======================================================================================================
        //. 確認
        //=======================================================================================================
        #region -- 確認

        /// <summary>
        /// 対象SpriteにSliceの設定があるか？
        /// </summary>
        /// <param name="_sprite">対象Sprite</param>
        /// <returns>True = あり, False = なし</returns>
        private bool CheckSliceSprite(Sprite _sprite) {
            if (_sprite == null)
                return false;

            var path = AssetDatabase.GetAssetPath(_sprite);
            var importer = (TextureImporter)TextureImporter.GetAtPath(path);

            return importer.spriteBorder != Vector4.zero;
        }

        #endregion

    }
}

#endif
