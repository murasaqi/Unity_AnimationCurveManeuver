using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using iridescent.util;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;
using Cursor = UnityEngine.UIElements.Cursor;
using Object = UnityEngine.Object;

namespace iridescent.AnimationCurveManeuver
{


    [Serializable]
    
    public struct AnimationCurveInfo
    {
        public AnimationCurve curve;
        public EditorCurveBinding binding;
        public string propertyName;
    }
    // public struct ExecutePointInfo
    // {
    //     public float time;
    //     public List<AnimationCurveInfo> AnimationCurveInfos;
    // }

    public class AnimationTrackBakerData
    {
        public PlayableDirector playableDirector;
        public Dictionary<AnimationTrack, bool> animationTracks;
        public bool applyOffset;
    }
    
    public class AnimationTrackBaker : EditorWindow
    {
        private AnimationTrackBakerData _data = new AnimationTrackBakerData();

        #region UI

        [MenuItem("Tools/AnimationTrackBaker")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(AnimationTrackBaker));
        }

        public void OnEnable()
        {

            var root = rootVisualElement;

            var objectfield = new ObjectField("PlayableDirector");
            objectfield.objectType = typeof(PlayableDirector);
            
            var executeInfoContainer = new VisualElement();
            var trackList = new VisualElement();
            var offsetToggle = new Toggle("Apply Offset");
            offsetToggle.RegisterValueChangedCallback(evt =>
            {
                _data.applyOffset = evt.newValue;
            });
            var selectButtonContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween
                }
            };
            var selectAllButton = new Button(() =>
            {
                var toggles = trackList.Query<Toggle>().ToList();
                foreach (var toggle in toggles)
                {
                    toggle.value = true;
                }
            });
            selectAllButton.style.flexGrow = 1;
            selectAllButton.text = "Select All";
            var deselectAllButton = new Button(() =>
            {
                var toggles = trackList.Query<Toggle>().ToList();
                foreach (var toggle in toggles)
                {
                    toggle.value = false;
                }
            });
            deselectAllButton.text = "Deselect All";
            deselectAllButton.style.flexGrow = 1;
            selectButtonContainer.Add(selectAllButton);
            selectButtonContainer.Add(deselectAllButton);
            
            executeInfoContainer.Add(trackList);
            executeInfoContainer.Add(offsetToggle);
            executeInfoContainer.Add(selectButtonContainer);
            
            // TrackListの初期化
            objectfield.RegisterValueChangedCallback(evt =>
            {
                trackList.Clear();
                
                _data.playableDirector = objectfield.value as PlayableDirector;
                if (_data.playableDirector == null)
                    return;
                
                SetAnimationTrackList(trackList);
            });

            var executeResultContainer = new VisualElement();

            var mergeButton = new Button(() =>
            {
                if (_data.playableDirector == null)
                {
                    Debug.LogError("No TimelineAsset");
                    return;
                }

                var playableDirector = _data.playableDirector;
                
                MergeClips(playableDirector,
                    _data.animationTracks.Where(val => val.Value).Select(val => val.Key).ToArray(),
                    _data.applyOffset);
              
                executeResultContainer.Clear();
            });
            mergeButton.text = "Merge";
            
            
            root.Add(objectfield);
            root.Add(executeInfoContainer);
            root.Add(mergeButton);
            root.Add(executeResultContainer);
        }

        private void SetAnimationTrackList(VisualElement trackList)
        {
            _data.animationTracks = new Dictionary<AnimationTrack, bool>();
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
                        backgroundColor = bgColor[cnt%2]
                    }
                };
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
                var label = new Label(name);
                
                trackField.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    trackField.style.backgroundColor = toggle.value ? selectedHoveredBgColor[cntCopy%2] : hoveredBgColor[cntCopy%2];
                });
                trackField.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    var color = trackField.style.backgroundColor.value;
                    trackField.style.backgroundColor =
                        toggle.value ? selectedBgColor[cntCopy%2] : bgColor[cntCopy%2];
                });
                trackField.RegisterCallback<ClickEvent>(evt =>
                {
                    toggle.value = !toggle.value;
                });

                trackField.Add(toggle);
                trackField.Add(label);
                
                trackList.Add(trackField);

                cnt++;
            }
        }

        #endregion

        #region Process

        private Dictionary<float,List<AnimationCurveInfo>> executeInfo = new Dictionary<float, List<AnimationCurveInfo>>();
        
        private void BakeAnimationClipFromAnimationTrack(PlayableDirector playableDirector, AnimationTrack track)
        {
            executeInfo.Clear();



            var trackBinding = playableDirector.GetGenericBinding(track) as GameObject;

            var timelineClips = track.GetClips();
            foreach (var timelineClip in timelineClips)
            {

                var exportClip = new AnimationClip();
                // track.timelineAsset.editorSettings.
                exportClip.name = track.name;
                exportClip.frameRate = (float) track.timelineAsset.editorSettings.frameRate;


                var curveBindingDict = new Dictionary<EditorCurveBinding, AnimationCurve>();
                
                var animationPlayableAsset = timelineClip.asset as AnimationPlayableAsset;
                var animationClip = animationPlayableAsset.clip;
                if (animationClip == null)
                {
                    continue;
                }

                // Bindingの取得
                var bindings = AnimationUtility.GetCurveBindings(animationClip);
                foreach (var binding in bindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(animationClip, binding);
                    var copyBinding = new EditorCurveBinding()
                    {
                        path = binding.path,
                        propertyName = binding.propertyName,
                        type = binding.type,
                    };

                    foreach (var key in curve.keys)
                    {

                        if (executeInfo.ContainsKey(key.time))
                        {
                            executeInfo[key.time].Add(new AnimationCurveInfo()
                            {
                                curve = curve,
                                binding = copyBinding,
                                propertyName = copyBinding.propertyName
                            });
                        }
                        else
                        {
                            executeInfo.Add(key.time, new List<AnimationCurveInfo>()
                            {
                                new AnimationCurveInfo()
                                {
                                    curve = curve,
                                    binding = copyBinding,
                                    propertyName = copyBinding.propertyName
                                }
                            });
                        }

                    }

                    curveBindingDict.Add(copyBinding, new AnimationCurve(new Keyframe[] { }));
                }

                // CurveのBake
                foreach (var (time, curveInfos) in executeInfo)
                {
                    playableDirector.time = time;
                    playableDirector.Evaluate();

                    foreach (var bindCurvePair in curveBindingDict)
                    {
                        var bind = bindCurvePair.Key;
                        var curve = bindCurvePair.Value;
                        Debug.Log(curve);
                        if (bind.propertyName == "m_LocalPosition.x")
                            curve.AddKey(time, trackBinding.transform.localPosition.x);
                        if (bind.propertyName == "m_LocalPosition.y")
                            curve.AddKey(time, trackBinding.transform.localPosition.y);
                        if (bind.propertyName == "m_LocalPosition.z")
                            curve.AddKey(time, trackBinding.transform.localPosition.z);
                        if (bind.propertyName == "m_LocalRotation.x")
                            curve.AddKey(time, trackBinding.transform.localRotation.x);
                        if (bind.propertyName == "m_LocalRotation.y")
                            curve.AddKey(time, trackBinding.transform.localRotation.y);
                        if (bind.propertyName == "m_LocalRotation.z")
                            curve.AddKey(time, trackBinding.transform.localRotation.z);

                    }
                }

                // Save
                AssetDatabase.CreateAsset(exportClip, "Assets/" + exportClip.name + ".anim");
                AnimationUtility.SetEditorCurves(exportClip, curveBindingDict.Keys.ToArray(),
                    curveBindingDict.Values.ToArray());
                AssetDatabase.SaveAssets();
            }
        }

        private void MergeClips(PlayableDirector playableDirector, AnimationTrack[] targetTracks, bool applyOffset)
        {
            var timeline = playableDirector.playableAsset as TimelineAsset;
            var mergedClip = new AnimationClip
            {
                name = $"{playableDirector.name}_merged",
                frameRate = (float) timeline.editorSettings.frameRate
            };
            
            var curveBinding = new Dictionary<EditorCurveBinding, AnimationCurve>();
            foreach (var track in targetTracks)
            {
                var trackCurveBinding = MergeClipsInTrack(track, playableDirector.duration, applyOffset, playableDirector);
                
                // トラックごとのBindingCurveをマージ
                var st = new StringBuilder();
                foreach (var d in trackCurveBinding)
                {
                    st.Append($"{d.Key.path}/{d.Key.propertyName}: {d.Value}\n");
                }
                Debug.Log(st);
                curveBinding = curveBinding.Concat(trackCurveBinding
                        .Where(pair => !curveBinding.ContainsKey(pair.Key)))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);

                st.Clear();
                foreach (var d in curveBinding)
                {
                    st.Append($"{d.Key.path}/{d.Key.propertyName}: {d.Value}\n");
                }
                Debug.Log(st);
            }
            
            AssetDatabase.CreateAsset(mergedClip, $"Assets/{mergedClip.name}.anim");
            AnimationUtility.SetEditorCurves(mergedClip, curveBinding.Keys.ToArray(),
                curveBinding.Values.ToArray());
            AssetDatabase.SaveAssets();
        }

        private Dictionary<EditorCurveBinding, AnimationCurve> MergeClipsInTrack(AnimationTrack track, double duration, bool applyOffset, PlayableDirector playableDirector)
        {
            var trackCurveBinding = new Dictionary<EditorCurveBinding, AnimationCurve>();
            
            var trackBindingObject = playableDirector.GetGenericBinding(track) as Animator;
            var clips = track.GetClips();
            foreach (var clip in clips)
            {
                var clipCurveBinding = new Dictionary<EditorCurveBinding, AnimationCurve>();
                
                var animationClip = (clip.asset as AnimationPlayableAsset).clip;
                if(animationClip == null)
                    continue;
                
                // Bindingとオフセット未適用のCurveを取得
                var bindings = AnimationUtility.GetCurveBindings(animationClip);
                foreach (var binding in bindings)
                {
                    // 親からのPathに変更
                    var newBinding = new EditorCurveBinding()
                    {
                        path = $"{trackBindingObject.gameObject.name}",
                        propertyName = binding.propertyName,
                        type = binding.type,
                    };
                    
                    var curve = AnimationUtility.GetEditorCurve(animationClip, binding);
                    
                    // 再生開始のオフセットを追加
                    var offsetKeyFrames = new Keyframe[curve.keys.Length];
                    for(var i = 0; i < curve.keys.Length; i++)
                    {
                        var keyframe = curve.keys[i];
                        offsetKeyFrames[i] = new Keyframe(keyframe.time + (float)track.start, keyframe.value);
                    }
                    
                    clipCurveBinding.Add(newBinding, new AnimationCurve(offsetKeyFrames));
                }
                
                // ApplyOffsetに応じてオフセット込みのCurveをBake
                if (applyOffset)
                {
                    var bakedCurveBinding = new Dictionary<EditorCurveBinding, AnimationCurve>();
                    foreach (var (binding, curve) in clipCurveBinding)
                    {
                        var bakedCurve = new AnimationCurve(new Keyframe[] { });

                        foreach (var key in curve.keys)
                        {
                            playableDirector.time = key.time;
                            playableDirector.Evaluate();
                            
                            if (binding.propertyName == "m_LocalPosition.x")
                                bakedCurve.AddKey(key.time, trackBindingObject.transform.localPosition.x);
                            if (binding.propertyName == "m_LocalPosition.y")
                                bakedCurve.AddKey(key.time, trackBindingObject.transform.localPosition.y);
                            if (binding.propertyName == "m_LocalPosition.z")
                                bakedCurve.AddKey(key.time, trackBindingObject.transform.localPosition.z);
                            if (binding.propertyName == "m_LocalRotation.x")
                                bakedCurve.AddKey(key.time, trackBindingObject.transform.localRotation.x);
                            if (binding.propertyName == "m_LocalRotation.y")
                                bakedCurve.AddKey(key.time, trackBindingObject.transform.localRotation.y);
                            if (binding.propertyName == "m_LocalRotation.z")
                                bakedCurve.AddKey(key.time, trackBindingObject.transform.localRotation.z);
                            // これEulerなのにQuaternionいれて大丈夫？
                        }
                        
                        bakedCurveBinding.Add(binding, bakedCurve);
                    }

                    clipCurveBinding = bakedCurveBinding;
                }
                
                // クリップごとのBindingCurveをマージ
                trackCurveBinding = trackCurveBinding.Concat(clipCurveBinding
                    .Where(pair => !trackCurveBinding.ContainsKey(pair.Key)))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
            }
            return trackCurveBinding;
        }

        #endregion
    }
}