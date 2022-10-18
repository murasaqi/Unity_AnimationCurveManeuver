using System;
using System.Collections.Generic;
using System.Linq;
using iridescent.util;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;
using Cursor = UnityEngine.UIElements.Cursor;

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
    }
    
    public class AnimationTrackBaker : EditorWindow
    {
        private AnimationTrackBakerData _data = new AnimationTrackBakerData();
        
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
            
            var executeInfoWrapper = new VisualElement();
            var trackList = new VisualElement();
            executeInfoWrapper.Add(trackList);
            
            objectfield.RegisterValueChangedCallback(evt =>
            {
                trackList.Clear();
                
                _data.playableDirector = objectfield.value as PlayableDirector;
                if (_data.playableDirector == null)
                    return;
                
                SetAnimationTrackList(trackList);
            });

            var fetchButton = new Button(() =>
            {
                if (_data.playableDirector == null)
                {
                    Debug.LogError("No TimelineAsset");
                    return;
                }

                var playableDirector = _data.playableDirector;
                var timelineAsset = playableDirector.playableAsset as TimelineAsset;

                var tracks = timelineAsset.GetOutputTracks();

                foreach (var track in tracks)
                {

                    if (track.GetType() == typeof(AnimationTrack))
                    {
                     
                        BakeAnimationClipFromAnimationTrack(playableDirector,track as AnimationTrack);   
                    }      
                }
              
                executeInfoWrapper.Clear();

                foreach (var e in executeInfo)
                {
                    
                    var container = new VisualElement();
                    var label = new Label(e.Key.ToString());
                    var label2 = new Label();
                    
                    foreach (var s in e.Value)
                    {
                        label2.text += s + " ";
                    }
                    container.Add(label);
                    container.Add(label2);
                    executeInfoWrapper.Add(container);
                }
            });
            fetchButton.text = "Merge";
            
            
            root.Add(objectfield);
            root.Add(executeInfoWrapper);
            root.Add(fetchButton);
        }

        private void SetAnimationTrackList(VisualElement trackList)
        {
            var tracks = TimelineUtil.GetAnimationTrackNameList(_data.playableDirector.playableAsset as TimelineAsset);
            var cnt = 0;
            foreach (var (name, track) in tracks)
            {
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
        
        
        private Dictionary<float,List<AnimationCurveInfo>> executeInfo = new Dictionary<float, List<AnimationCurveInfo>>();

        

        private void BakeAnimationClipFromAnimationTrack(PlayableDirector playableDirector,AnimationTrack track)
        {
            executeInfo.Clear();
            
           
            
            var trackBinding = playableDirector.GetGenericBinding(track) as GameObject;
            
            var timelineClips = track.GetClips();
            foreach (var timelineClip in timelineClips)
            {
                
                var exportClip = new AnimationClip();
                // track.timelineAsset.editorSettings.
                exportClip.name = track.name;
                exportClip.frameRate = (float)track.timelineAsset.editorSettings.frameRate;

                
                var curveBindingDict = new Dictionary<EditorCurveBinding,AnimationCurve>();
                var animationPlayableAsset = timelineClip.asset as AnimationPlayableAsset;

                var animationClip = animationPlayableAsset.clip;
                if (animationClip == null)
                {
                    continue;
                }

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
                    curveBindingDict.Add( copyBinding,new AnimationCurve(new Keyframe[]{}));
                }
                
                foreach (var e in executeInfo)
                {
                    playableDirector.time = e.Key;
                    playableDirector.Evaluate();
                
                    foreach (var bindCurvePair in curveBindingDict)
                    {
                        var bind = bindCurvePair.Key;
                        var curve =bindCurvePair.Value;
                        Debug.Log(curve);
                        if(bind.propertyName == "m_LocalPosition.x")
                            curve.AddKey(e.Key,trackBinding.transform.localPosition.x);
                        if(bind.propertyName == "m_LocalPosition.y")
                            curve.AddKey(e.Key,trackBinding.transform.localPosition.y);
                        if(bind.propertyName == "m_LocalPosition.z")
                            curve.AddKey(e.Key,trackBinding.transform.localPosition.z);
                        if(bind.propertyName == "m_LocalRotation.x")
                            curve.AddKey(e.Key,trackBinding.transform.localRotation.x);
                        if(bind.propertyName == "m_LocalRotation.y")
                            curve.AddKey(e.Key,trackBinding.transform.localRotation.y);
                        if(bind.propertyName == "m_LocalRotation.z")
                            curve.AddKey(e.Key,trackBinding.transform.localRotation.z);
                    
                    }
                }
            
                AssetDatabase.CreateAsset(exportClip, "Assets/" + exportClip.name + ".anim");
                AnimationUtility.SetEditorCurves(exportClip,curveBindingDict.Keys.ToArray(),curveBindingDict.Values.ToArray());
                AssetDatabase.SaveAssets();
            }

           
            
        }

    }
}