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
    public enum OffsetMode
    {
        Euler,
        Quaternion
    }

    public class AnimationTrackBakerData
    {
        public PlayableDirector playableDirector;
        public Dictionary<AnimationTrack, bool> animationTracks;
        public bool applyOffset;
        public OffsetMode offsetMode;
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

            var mergeButton = root.Q<Button>("MergeButton");
            mergeButton.RegisterCallback<ClickEvent>(evt =>
            {
                if (_data.playableDirector == null)
                {
                    Debug.LogError("No TimelineAsset");
                    return;
                }

                var playableDirector = _data.playableDirector;
                
                MergeClips(playableDirector,
                    _data.animationTracks.Where(val => val.Value).Select(val => val.Key).ToArray(),
                    _data.applyOffset, _data.offsetMode);
            });
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
                toggle.focusable = false;
                toggle.pickingMode = PickingMode.Ignore;
                toggle.RegisterCallback<ClickEvent>(evt => toggle.value = !toggle.value); // トグルをクリックしたときに選択が上書きされてしまう問題への対処
                var label = new Label($"{name} ({_data.playableDirector.GetGenericBinding(track).name})");
                
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

        // 渡されたトラック全てを1クリップにマージ
        private void MergeClips(PlayableDirector playableDirector, AnimationTrack[] targetTracks, bool applyOffset, OffsetMode offsetMode)
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
                var trackCurveBinding = MergeClipsInTrack(track, playableDirector.duration, applyOffset, offsetMode, playableDirector);
                
                // トラックごとのBindingCurveをマージ
                curveBinding = curveBinding.Concat(trackCurveBinding
                        .Where(pair => !curveBinding.ContainsKey(pair.Key)))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
            }
            
            AssetDatabase.CreateAsset(mergedClip, $"Assets/{mergedClip.name}.anim");
            AnimationUtility.SetEditorCurves(mergedClip, curveBinding.Keys.ToArray(),
                curveBinding.Values.ToArray());
            AssetDatabase.SaveAssets();
        }

        // トラックごとのClipのマージ
        private Dictionary<EditorCurveBinding, AnimationCurve> MergeClipsInTrack(AnimationTrack track, double duration, bool applyOffset, OffsetMode offsetMode, PlayableDirector playableDirector)
        {
            var trackCurveBinding = new Dictionary<EditorCurveBinding, AnimationCurve>();
            
            var trackBindingObject = playableDirector.GetGenericBinding(track) as Animator;
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
                    var path = trackBindingObject.gameObject.name + (string.IsNullOrEmpty(binding.path)
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
                        var prevKeyframe =
                            i != 0 ? curve.keys[i] : new Keyframe(curve.keys[i].time - 1.0f, curve.keys[i].value);
                        var keyframe = curve.keys[i];
                        var keyframeTime = keyframe.time / (float)clip.timeScale;

                        if(clip.duration < keyframeTime) break;

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

                    clipCurveBinding.Add(newBinding, offsetCurves);
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