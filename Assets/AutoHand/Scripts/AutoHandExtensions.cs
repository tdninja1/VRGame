using Autohand;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Autohand
{
    public static class AutoHandExtensions
    {
        
        static Transform _transformRuler = null;
        //This is a "ruler" tool used to help calculate parent child calculations without doing parenting/childing
        public static Transform transformRuler
        {
            get {
                if(_transformRuler == null)
                    _transformRuler = new GameObject().transform;

                if (_transformRuler.parent != null)
                    _transformRuler.parent = null;

                if (_transformRuler.localScale != Vector3.one)
                    _transformRuler.localScale = Vector3.one;



                return _transformRuler;
            }
        }

        static Transform _transformRulerChild = null;
        //This is a "ruler" tool used to help calculate parent child calculations without doing parenting/childing
        public static Transform transformRulerChild
        {
            get {
                if (_transformRulerChild == null)
                {
                    _transformRulerChild = new GameObject().transform;
                    _transformRulerChild.parent = _transformRuler;
                }

                if (_transformRulerChild.parent != _transformRuler)
                    _transformRulerChild.parent = _transformRuler;

                if (_transformRulerChild.localScale != Vector3.one)
                    _transformRulerChild.localScale = Vector3.one;

                return _transformRulerChild;
            }
        }


        public static float Round(this float value, int digits)
        {
            float mult = Mathf.Pow(10.0f, (float)digits);
            return Mathf.Round(value * mult) / mult;
        }

        /// <summary>Returns true if there is a grabbable or link, out null if there is none</summary>
        public static bool HasGrabbable(this Hand hand, GameObject obj, out Grabbable grabbable)
        {
            return HasGrabbable(obj, out grabbable);
        }

        /// <summary>Returns true if there is a grabbable or link, out null if there is none</summary>
        public static bool HasGrabbable(this GameObject obj, out Grabbable grabbable)
        {
            if (obj == null)
            {
                grabbable = null;
                return false;
            }

            if (obj.CanGetComponent(out grabbable))
            {
                return true;
            }

            GrabbableChild grabChild;
            if (obj.CanGetComponent(out grabChild))
            {
                grabbable = grabChild.grabParent;
                return true;
            }

            grabbable = null;
            return false;
        }









        public static T GetCopyOf<T>(this Component comp, T other) where T : Component{
            Type type = comp.GetType();
            if (type != other.GetType()) return null; // type mis-match
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default | BindingFlags.DeclaredOnly;
            PropertyInfo[] pinfos = type.GetProperties(flags);
            foreach (var pinfo in pinfos)
            {
                if (pinfo.CanWrite)
                {
                    try
                    {
                        pinfo.SetValue(comp, pinfo.GetValue(other, null), null);
                    }
                    catch { } // In case of NotImplementedException being thrown. For some reason specifying that exception didn't seem to catch it, so I didn't catch anything specific.
                }
            }
            FieldInfo[] finfos = type.GetFields(flags);
            foreach (var finfo in finfos)
            {
                finfo.SetValue(comp, finfo.GetValue(other));
            }
            return comp as T;
        }



        /// <summary>Autohand extension method, used so I can use TryGetComponent for newer versions and GetComponent for older versions</summary>
        public static bool CanGetComponent<T>(this Component componentClass, out T component)
        {
#if UNITY_2019_1 || UNITY_2018 || UNITY_2017
       var tempComponent = componentClass.GetComponent<T>();
        if(tempComponent != null){
            component = tempComponent;
            return true;
        }
        else {
            component = tempComponent;
            return false;
        }
#else
            var value = componentClass.TryGetComponent(out component);
            return value;
#endif
        }

        /// <summary>Autohand extension method, used so I can use TryGetComponent for newer versions and GetComponent for older versions</summary>
        public static bool CanGetComponent<T>(this GameObject componentClass, out T component)
        {
#if UNITY_2019_1 || UNITY_2018 || UNITY_2017
       var tempComponent = componentClass.GetComponent<T>();
        if(tempComponent != null){
            component = tempComponent;
            return true;
        }
        else {
            component = tempComponent;
            return false;
        }
#else
            var value = componentClass.TryGetComponent(out component);
            return value;
#endif
        }


#if UNITY_EDITOR

        public static GUIStyle LabelStyle(TextAnchor textAnchor = TextAnchor.MiddleLeft, FontStyle fontStyle = FontStyle.Normal, int fontSize = 13) {
            var style = new GUIStyle(GUI.skin.label);
            style.font = (Font)Resources.Load("Righteous-Regular", typeof(Font));
            style.fontSize = fontSize;
            style.alignment = textAnchor;
            style.fontStyle = fontStyle;
            return style;
        }
        public static GUIStyle LabelStyle(Color textColor, TextAnchor textAnchor = TextAnchor.MiddleLeft, FontStyle fontStyle = FontStyle.Normal, int fontSize = 13) {
            var style = new GUIStyle(GUI.skin.label);
            style.font = (Font)Resources.Load("Righteous-Regular", typeof(Font));
            style.fontSize = fontSize;
            style.alignment = textAnchor;
            style.fontStyle = fontStyle;
            style.normal.textColor = textColor;
            return style;
        }

#endif
    }

}