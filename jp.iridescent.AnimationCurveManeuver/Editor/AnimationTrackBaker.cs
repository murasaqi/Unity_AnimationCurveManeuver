using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using iridescent.util;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace iridescent.AnimationCurveManeuver
{
    public enum OffsetMode
    {
        Euler,
        Quaternion
    }

    public enum ExportMode
    {
        AllMerge,
        PerTrack,
    }

    public class AnimationTrackBakerData
    {
        public PlayableDirector playableDirector;
        public GameObject rootGameObject;
        public Dictionary<AnimationTrack, bool> animationTracks;
        public bool applyOffset;
        public OffsetMode offsetMode;
        public string exportFilePath;
        public string exportFolderPath;
        public ExportMode exportMode;
    }

    public struct KeyFrameTangentMode
    {
        public bool broken;
        public AnimationUtility.TangentMode leftTangent;
        public AnimationUtility.TangentMode rightTangent;
    }
    
    public class AnimationTrackBaker : EditorWindow
    {
        [SerializeField, HideInInspector] private VisualTreeAsset xml;
        
        private AnimationTrackBakerData _data = new AnimationTrackBakerData();

        private List<Tuple<AnimationTrack, VisualElement>> trackElementPairs;
        private VisualElement multipleSelectionStartElement = null;

        #region UI

        [MenuItem("Tools/AnimationTrackBaker")]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(AnimationTrackBaker));
            window.titleContent = new GUIContent("Animation Track Baker");
        }

        public void OnEnable()
        {

            var root = rootVisualElement;
            
            root.Add(xml.CloneTree());

            var rootObjectField = root.Q<ObjectField>("RootGameObject");
            rootObjectField.RegisterValueChangedCallback(evt =>
            {
                _data.rootGameObject = evt.newValue as GameObject;
            });
            
            var trackList = root.Q<VisualElement>("TrackList");
            
            var offsetToggle = root.Q<Toggle>("DirtyOffsetToggle");
            offsetToggle.RegisterValueChangedCallback(evt =>
            {
                _data.applyOffset = evt.newValue;
            });
            
            var offsetMode = root.Q<EnumField>("OffsetRotationMethod");
            offsetMode.RegisterValueChangedCallback(evt =>
            {
                _data.offsetMode = (OffsetMode)evt.newValue;
            });
            
            var selectAllButton = root.Q<Button>("SelectAllButton");
            selectAllButton.RegisterCallback<ClickEvent>(evt =>
            {
                var toggles = trackList.Query<Toggle>().ToList();
                foreach (var toggle in toggles)
                {
                    toggle.value = true;
                }
            });
            
            var deselectAllButton = root.Q<Button>("DeselectAllButton");
            deselectAllButton.RegisterCallback<ClickEvent>(evt =>
            {
                var toggles = trackList.Query<Toggle>().ToList();
                foreach (var toggle in toggles)
                {
                    toggle.value = false;
                }
            });
            
            // TrackListの初期化
            var playableDirectorField = root.Q<ObjectField>("PlayableDirectorField");
            playableDirectorField.RegisterValueChangedCallback(evt =>
            {
                trackList.Clear();
                
                _data.playableDirector = playableDirectorField.value as PlayableDirector;
                if (_data.playableDirector == null)
                    return;
                
                SetAnimationTrackList(trackList);
            });

            // 出力ファイルのパス設定
            var exportFilePathField = root.Q<TextField>("ExportFilePath");
            const string exportFilePathLabel = "Export File Path";
            const string exportFolderPathLabel = "Export Folder Path";
            exportFilePathField.RegisterValueChangedCallback(evt =>
            {
                if (_data.exportMode == ExportMode.AllMerge)
                {
                    _data.exportFilePath = exportFilePathField.value;
                    if (File.Exists($"{Path.GetDirectoryName(Application.dataPath)}/{_data.exportFilePath}"))
                    {
                        exportFilePathField.label = $"{exportFilePathLabel} *";
                    }
                    else
                    {
                        exportFilePathField.label = exportFilePathLabel;
                    }
                }
                else
                {
                    _data.exportFolderPath = exportFilePathField.value;
                    if (Directory.Exists($"{Path.GetDirectoryName(Application.dataPath)}/{_data.exportFolderPath}"))
                    {
                        exportFilePathField.label = $"{exportFolderPathLabel} *";
                    }
                    else
                    {
                        exportFilePathField.label = exportFolderPathLabel;
                    }
                }
            });
            var pathSelectorButton = root.Q<Button>("PathSelectorButton");
            pathSelectorButton.RegisterCallback<ClickEvent>(evt =>
            {
                string result;
                if (_data.exportMode == ExportMode.AllMerge)
                {
                    result = EditorUtility.SaveFilePanelInProject("Select file path to Export",
                        Path.GetFileName(_data.exportFilePath) ?? "Merged.anim", "anim", "Select file path to Export in Project.", Path.GetDirectoryName(_data.exportFilePath) ?? "");
                }
                else
                {
                    result = EditorUtility.SaveFolderPanel("Select file path to Export", _data.exportFolderPath, Path.GetFileName(_data.exportFolderPath));
                    result = result.Replace($"{Application.dataPath}/", "Assets/");
                }
                
                if (!string.IsNullOrEmpty(result))
                {
                    exportFilePathField.value = result;
                }
            });
            
            // 出力モード
            var exportModeField = root.Q<EnumField>("ExportMode");
            exportModeField.RegisterValueChangedCallback(evt =>
            {
                _data.exportMode = (ExportMode)exportModeField.value;

                if (_data.exportMode == ExportMode.AllMerge)
                {
                    exportFilePathField.label = exportFilePathLabel;
                    exportFilePathField.value = _data.exportFilePath;
                }
                else
                {
                    exportFilePathField.label = exportFolderPathLabel;
                    exportFilePathField.value = _data.exportFolderPath;
                }
            });
            
            // 結果出力フィールド
            var resultContainer = root.Q<VisualElement>("ResultContainer");
            var resultField = root.Q<VisualElement>("Result");

            // 出力ボタン
            var mergeButton = root.Q<Button>("ExportButton");
            mergeButton.RegisterCallback<ClickEvent>(evt =>
            {
                if (!CheckAllParameterSet())
                {
                    return;
                }
                
                resultField.Clear();
                resultContainer.style.display = DisplayStyle.None;

                var playableDirector = _data.playableDirector;

                AnimationClip[] results;
                // AllMerge
                if (_data.exportMode == ExportMode.AllMerge)
                {
                    results = ExportMergedClip(playableDirector,
                        _data.animationTracks.Where(val => val.Value).Select(val => val.Key).ToArray(),
                        _data.rootGameObject.transform, _data.applyOffset, _data.offsetMode, _data.exportFilePath);
                }
                // PerTracks
                else
                {
                    results = ExportClipsPerTrack(playableDirector,
                        _data.animationTracks.Where(val => val.Value).Select(val => val.Key).ToArray(),
                        _data.rootGameObject.transform, _data.applyOffset, _data.offsetMode, _data.exportFolderPath);
                }

                // 結果表示
                if (results != null)
                {
                    resultContainer.style.display = DisplayStyle.Flex;
                    foreach (var result in results)
                    {
                        resultField.Add(new ObjectField(result.name){value = result});
                    }
                }
            });

            /*
            var exportTracksButton = root.Q<Button>("ExportTracksButton");
            exportTracksButton.RegisterCallback<ClickEvent>(evt =>
            {
                if (_data.playableDirector == null)
                {
                    Debug.LogError("No TimelineAsset");
                    return;
                }

                var playableDirector = _data.playableDirector;
                
                ExportTracks(playableDirector,
                    _data.animationTracks.Where(val => val.Value).Select(val => val.Key).ToArray(),
                    _data.rootGameObject.transform, _data.applyOffset, _data.offsetMode, _data.exportFilePath);
            });
            */
        }

        private void SetAnimationTrackList(VisualElement trackList)
        {
            _data.animationTracks = new Dictionary<AnimationTrack, bool>();
            trackElementPairs = new List<Tuple<AnimationTrack, VisualElement>>();
            
            var tracks = TimelineUtil.GetAnimationTrackNameList(_data.playableDirector.playableAsset as TimelineAsset);
            var cnt = 0;
            foreach (var (name, track) in tracks)
            {
                _data.animationTracks.Add(track, false);
                
                var bgColor = new Color[2];
                ColorUtility.TryParseHtmlString("#383838", out bgColor[0]);
                ColorUtility.TryParseHtmlString("#4c4c4c", out bgColor[1]);
                var hoveredBgColor = new Color[2];
                ColorUtility.TryParseHtmlString("#424242", out hoveredBgColor[0]);
                ColorUtility.TryParseHtmlString("#565656", out hoveredBgColor[1]);
                var selectedBgColor = new Color[2];
                ColorUtility.TryParseHtmlString("#9267ca", out selectedBgColor[0]);
                ColorUtility.TryParseHtmlString("#ad7af0", out selectedBgColor[1]);
                var selectedHoveredBgColor = new Color[2];
                ColorUtility.TryParseHtmlString("#6d4c96", out selectedHoveredBgColor[0]);
                ColorUtility.TryParseHtmlString("#6d4c96", out selectedHoveredBgColor[1]);

                ColorUtility.TryParseHtmlString("#c3c3c3", out var textColor);
                ColorUtility.TryParseHtmlString("#545454", out var mutedTextColor);

                var muted = track.muted || (track.GetGroup()?.muted ?? false);
                
                var trackField = new VisualElement
                {
                    style =
                    {
                        marginLeft = 3,
                        marginRight = 3,
                        paddingTop = 3,
                        paddingBottom = 3,
                        flexDirection = FlexDirection.Row,
                        justifyContent = Justify.FlexStart,
                        backgroundColor = bgColor[cnt%2],
                        color = muted ? mutedTextColor : textColor,
                    }
                };
                trackField.pickingMode = muted ? PickingMode.Ignore : PickingMode.Position;
                var toggle = new Toggle
                {
                    style =
                    {
                        marginRight = 10,
                    }
                };
                var cntCopy = cnt;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    trackField.style.backgroundColor = evt.newValue ? selectedBgColor[cntCopy%2] : bgColor[cntCopy%2];
                    
                    _data.animationTracks[track] = evt.newValue;
                });
                toggle.focusable = false;
                toggle.pickingMode = PickingMode.Ignore;
                toggle.Children().First().pickingMode = PickingMode.Ignore;
                toggle.RegisterCallback<ClickEvent>(evt => toggle.value = !toggle.value); // トグルをクリックしたときに選択が上書きされてしまう問題への対処
                var label = new Label($"{name} ({_data.playableDirector.GetGenericBinding(track).name})");

                if (!muted)
                {
                    trackField.RegisterCallback<MouseEnterEvent>(evt =>
                    {
                        trackField.style.backgroundColor = toggle.value ? selectedHoveredBgColor[cntCopy%2] : hoveredBgColor[cntCopy%2];
                    });
                    trackField.RegisterCallback<MouseLeaveEvent>(evt =>
                    {
                        trackField.style.backgroundColor =
                            toggle.value ? selectedBgColor[cntCopy%2] : bgColor[cntCopy%2];
                    });
                    trackField.RegisterCallback<ClickEvent>(evt =>
                    {
                        toggle.value = !toggle.value;

                        if (evt.shiftKey)
                        {
                            if(multipleSelectionStartElement != null)
                            {
                                ApplyMultipleSelectionToggle(multipleSelectionStartElement, trackField);
                            }
                        }
                        multipleSelectionStartElement = trackField;
                    });
                }

                trackField.Add(toggle);
                trackField.Add(label);
                
                trackList.Add(trackField);
                
                trackElementPairs.Add(new Tuple<AnimationTrack, VisualElement>(track, trackField));

                cnt++;
            }
        }
        
        // 複数選択の適用
        private void ApplyMultipleSelectionToggle(VisualElement startElement, VisualElement endElement)
        {
            var startIndex = trackElementPairs.FindIndex(pair => pair.Item2 == startElement);
            var endIndex = trackElementPairs.FindIndex(pair => pair.Item2 == endElement);
            if (endIndex < startIndex)
            {
                (endIndex, startIndex) = (startIndex, endIndex);
            }
            for (var i = startIndex+1; i < endIndex; i++)
            {
                var track = trackElementPairs[i].Item1;
                if (track.muted || (track.GetGroup()?.muted ?? false))
                {
                    continue;
                }
                (trackElementPairs[i].Item2.Children().First() as Toggle).value = true;
            }
        }
        
        // 出力実行時のパラメータ検証
        private bool CheckAllParameterSet()
        {
            if (_data.playableDirector == null)
            {
                Debug.LogError("Playable Director not set.");
                return false;
            }

            if (_data.rootGameObject == null)
            {
                Debug.LogError("Root GameObject not set.");
                return false;
            }
            
            if (!_data.animationTracks.ContainsValue(true))
            {
                Debug.LogError("No tracks selected.");
                return false;
            }
            
            if (_data.exportMode == ExportMode.AllMerge && string.IsNullOrEmpty(_data.exportFilePath))
            {
                Debug.LogError("Export File Path not set.");
                return false;
            }

            if (_data.exportMode == ExportMode.PerTrack && string.IsNullOrEmpty(_data.exportFolderPath))
            {
                Debug.LogError("Export Folder Path not set.");
                return false;
            }

            return true;
        }

        #endregion

        #region Process

        // 渡されたトラック全てを1クリップにマージ
        private AnimationClip[] ExportMergedClip(PlayableDirector playableDirector, AnimationTrack[] targetTracks, Transform root, bool applyOffset, OffsetMode offsetMode, string exportPath)
        {
            var curveBinding = new Dictionary<EditorCurveBinding, AnimationCurve>();
            foreach (var track in targetTracks)
            {
                var trackBindingObject = playableDirector.GetGenericBinding(track) as Animator;
                var pathToObject = GetPath(root, trackBindingObject.transform);
                if (pathToObject == null)
                {
                    Debug.LogError($"Path Not Found from root to track binding object.");
                    return null;
                }
                var trackCurveBinding = MergeClipsInTrack(track, pathToObject, (float)playableDirector.duration, applyOffset, offsetMode, playableDirector);
                
                // トラックごとのBindingCurveをマージ
                curveBinding = MergeBindingCurveDictionaries(curveBinding, trackCurveBinding);
            }
            
            // ファイル出力
            var timeline = playableDirector.playableAsset as TimelineAsset;
            var mergedClip = new AnimationClip
            {
                name = Path.GetFileName(exportPath),
                frameRate = (float) timeline.editorSettings.frameRate,
            };
            
            AnimationUtility.SetEditorCurves(mergedClip, curveBinding.Keys.ToArray(),
                curveBinding.Values.ToArray());
            
            EditorUtility.SetDirty(mergedClip);
            TimelineUtil.CreateAnimationClipAssetWithOverwrite(ref mergedClip, exportPath);

            return new []{mergedClip};
        }

        // 渡されたトラックをトラックごとにクリップにして出力
        private AnimationClip[] ExportClipsPerTrack(PlayableDirector playableDirector, AnimationTrack[] targetTracks,
            Transform root, bool applyOffset, OffsetMode offsetMode, string exportPath)
        {
            var results = new List<AnimationClip>();

            var i = 0;
            foreach (var track in targetTracks)
            {
                var curveBinding = new Dictionary<EditorCurveBinding, AnimationCurve>();
                
                var trackBindingObject = playableDirector.GetGenericBinding(track) as Animator;
                var pathToObject = GetPath(root, trackBindingObject.transform);
                if (pathToObject == null)
                {
                    Debug.LogError($"Path Not Found from root to track binding object.");
                    continue;
                }
                var trackCurveBinding = MergeClipsInTrack(track, pathToObject, (float)playableDirector.duration, applyOffset, offsetMode, playableDirector);
                
                // トラックごとのBindingCurveをマージ
                curveBinding = MergeBindingCurveDictionaries(curveBinding, trackCurveBinding);
                
                var timeline = playableDirector.playableAsset as TimelineAsset;
                var mergedClip = new AnimationClip
                {
                    name = track.name,
                    frameRate = (float) timeline.editorSettings.frameRate,
                };
            
                AnimationUtility.SetEditorCurves(mergedClip, curveBinding.Keys.ToArray(),
                    curveBinding.Values.ToArray());
            
                EditorUtility.SetDirty(mergedClip);
                TimelineUtil.CreateAnimationClipAssetWithOverwrite(ref mergedClip, $"{exportPath}/{track.name}.anim");
                
                results.Add(mergedClip);

                i++;
            }

            return results.ToArray();
        }

        // トラックごとのClipのマージ
        private Dictionary<EditorCurveBinding, AnimationCurve> MergeClipsInTrack(AnimationTrack track, string pathToTrackObject, float duration, bool applyOffset, OffsetMode offsetMode, PlayableDirector playableDirector)
        {
            var trackCurveBinding = new Dictionary<EditorCurveBinding, AnimationCurve>();
            
            var clips = track.GetClips();
            foreach (var clip in clips)
            {
                var clipCurveBinding = new Dictionary<EditorCurveBinding, AnimationCurve>();

                var animationPlayableAsset = clip.asset as AnimationPlayableAsset;
                var animationClip = animationPlayableAsset.clip;
                if(animationClip == null)
                    continue;

                // TrackBindingのAnimatorのTransformのBindingのKeyframeを事前取得
                // オフセット適用時のPosition Curveに必要
                var bindings = AnimationUtility.GetCurveBindings(animationClip);
                var offsetPosition = animationPlayableAsset.position;
                var offsetRotation = animationPlayableAsset.rotation;
                var positionCurves = new[]{new AnimationCurve(), new AnimationCurve(), new AnimationCurve()};
                var rotationCurves = new[]{new AnimationCurve(), new AnimationCurve(), new AnimationCurve()};
                foreach (var binding in bindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(animationClip, binding);
                    
                    if (applyOffset)
                    {
                        var timeScaledCurve = new AnimationCurve(curve.keys.Select(keyframe => new Keyframe
                        {
                            time = keyframe.time / (float)clip.timeScale,
                            value = keyframe.value,
                            inTangent = keyframe.inTangent,
                            inWeight = keyframe.inWeight,
                            outTangent = keyframe.outTangent,
                            outWeight = keyframe.outWeight,
                            weightedMode = keyframe.weightedMode
                        }).ToArray());
                        
                        if (binding.propertyName == "m_LocalPosition.x")
                        {
                            positionCurves[0] = timeScaledCurve;
                        }

                        if (binding.propertyName == "m_LocalPosition.y")
                        {
                            positionCurves[1] = timeScaledCurve;
                        }

                        if (binding.propertyName == "m_LocalPosition.z")
                        {
                            positionCurves[2] = timeScaledCurve;
                        }
                        
                        if (binding.propertyName == "localEulerAnglesRaw.x")
                        {
                            rotationCurves[0] = timeScaledCurve;
                        }
                        if (binding.propertyName == "localEulerAnglesRaw.y")
                        {
                            rotationCurves[1] =  timeScaledCurve;
                        }
                        if (binding.propertyName == "localEulerAnglesRaw.z")
                        {
                            rotationCurves[2] =  timeScaledCurve;
                        }
                    }
                }

                // BindingとCurveの加工
                foreach (var binding in bindings)
                {
                    // 親からのPathに変更
                    var path = pathToTrackObject + (string.IsNullOrEmpty(binding.path)
                        ? ""
                        : $"/{binding.path}");
                    var newBinding = new EditorCurveBinding()
                    {
                        path = path,
                        propertyName = binding.propertyName,
                        type = binding.type,
                    };
                    
                    var curve = AnimationUtility.GetEditorCurve(animationClip, binding);
                    
                    // TangentModeの取得
                    var tangentModes = new KeyFrameTangentMode[curve.keys.Length];
                    for (var i = 0; i < curve.keys.Length; i++)
                    {
                        tangentModes[i] = new KeyFrameTangentMode
                        {
                            broken = AnimationUtility.GetKeyBroken(curve, i),
                            leftTangent = AnimationUtility.GetKeyLeftTangentMode(curve, i),
                            rightTangent = AnimationUtility.GetKeyRightTangentMode(curve, i)
                        };
                    }
                    
                    // AnimationCurveの加工
                    var offsetKeyFrames = new Keyframe[curve.keys.Length];
                    for(var i = 0; i < curve.keys.Length; i++)
                    {
                        var keyframe = curve.keys[i];
                        var keyframeTime = keyframe.time / (float)clip.timeScale;

                        //if(clip.duration < keyframeTime) break;

                        // ApplyOffsetに応じてオフセット込みのValueを作成
                        var value = 0f;
                        if (applyOffset)
                        {
                            var position = new Vector3(positionCurves[0].Evaluate(keyframeTime), positionCurves[1].Evaluate(keyframeTime), positionCurves[2].Evaluate(keyframeTime));
                            var rotation = new Vector3(rotationCurves[0].Evaluate(keyframeTime), rotationCurves[1].Evaluate(keyframeTime), rotationCurves[2].Evaluate(keyframeTime));
                            var resultRotation = offsetMode switch
                            {
                                OffsetMode.Euler => offsetRotation.eulerAngles + rotation,
                                OffsetMode.Quaternion => Quaternion
                                    .Normalize(Quaternion.Euler(rotation) * offsetRotation).eulerAngles,
                                _ => throw new InvalidOperationException()
                            };
                            
                            if (binding.propertyName == "localEulerAnglesRaw.x")
                            {
                                value = resultRotation.x;
                            }
                            else if (binding.propertyName == "localEulerAnglesRaw.y")
                            {
                                value = resultRotation.y;
                            }
                            else if (binding.propertyName == "localEulerAnglesRaw.z")
                            {
                                value = resultRotation.z;
                            }
                            else if (binding.propertyName == "m_LocalPosition.x")
                            {
                                value = offsetPosition.x + (offsetRotation * position).x;
                            }
                            else if (binding.propertyName == "m_LocalPosition.y")
                            {
                                value = offsetPosition.y + (offsetRotation * position).y;
                            }
                            else if (binding.propertyName == "m_LocalPosition.z")
                            {
                                value = offsetPosition.z + (offsetRotation * position).z;
                            }
                            else
                            {
                                value = keyframe.value;
                            }
                        }
                        else
                        {
                            value = keyframe.value;
                        }
                        
                        offsetKeyFrames[i] = new Keyframe
                        {
                            time = keyframeTime + (float)clip.start,
                            value = value,
                            inTangent = keyframe.inTangent,
                            inWeight = keyframe.inWeight,
                            outTangent = keyframe.outTangent,
                            outWeight = keyframe.outWeight,
                            weightedMode = keyframe.weightedMode
                        };
                    }
                    
                    // TangentModeの適用
                    var offsetCurves = new AnimationCurve(offsetKeyFrames);
                    for (var i = 0; i < curve.keys.Length; i++)
                    {
                        AnimationUtility.SetKeyBroken(offsetCurves, i, tangentModes[i].broken);
                        AnimationUtility.SetKeyLeftTangentMode(offsetCurves, i, tangentModes[i].leftTangent);
                        AnimationUtility.SetKeyRightTangentMode(offsetCurves, i, tangentModes[i].rightTangent);
                    }
                    
                    // 0とduration位置にキーを打つ
                    AnimationCurveUtilityWrapper.AddBetweenKey(offsetCurves, 0);
                    AnimationCurveUtilityWrapper.AddBetweenKey(offsetCurves, duration);

                    clipCurveBinding.Add(newBinding, offsetCurves);
                }
                
                // クリップごとのBindingCurveをマージ
                trackCurveBinding = MergeBindingCurveDictionaries(trackCurveBinding, clipCurveBinding);

            }
            return trackCurveBinding;
        }

        #endregion

        #region Utils

        private string GetPath(Transform rootTrans, Transform targetTrans)
        {
            string GetPathRecursive(Transform baseTrans, Transform targetTrans, string path)
            {
                if (baseTrans.name == targetTrans.name) return path;
                var childrenCount = baseTrans.childCount;
                for (var i = 0; i < childrenCount; i++)
                {
                    var child = baseTrans.GetChild(i);
                    var result = GetPathRecursive(child, targetTrans, (string.IsNullOrEmpty(path) ? "" : $"{path}/") + child.name);
                    if (result != null)
                        return result;
                }

                return null;
            }

            return  GetPathRecursive(rootTrans, targetTrans, "");
        }

        public class KeyframeComparer : IEqualityComparer<Keyframe>
        {
            public bool Equals(Keyframe lhs, Keyframe rhs)
            {
                return Mathf.Approximately(lhs.time, rhs.time);
            }

            public int GetHashCode(Keyframe obj)
            {
                return obj.GetHashCode();
            }
        }

        private Dictionary<EditorCurveBinding, AnimationCurve> MergeBindingCurveDictionaries(Dictionary<EditorCurveBinding, AnimationCurve> dictionary1,
            Dictionary<EditorCurveBinding, AnimationCurve> dictionary2)
        {
            var merged = dictionary1.ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (var (binding, curve) in dictionary2)
            {
                if (merged.ContainsKey(binding))
                {
                    merged[binding] =
                        new AnimationCurve(merged[binding].keys.Concat(curve.keys).Distinct(new KeyframeComparer()).ToArray());
                }
                else
                {
                    merged.Add(binding, curve);
                }
            }

            return merged;
        }

        #endregion
    }
}