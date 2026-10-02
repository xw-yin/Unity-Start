using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Homecoming
{
    public sealed class HomecomingCameraController : MonoBehaviour
    {
        CharacterRig rig;
        Camera cameraView;
        float yaw, pitch, distance, previousPinch;
        int primaryFinger=-1, pinchFingerA=-1, pinchFingerB=-1;
        bool initialized, mouseBlocked;
        readonly HashSet<int> blockedFingers=new HashSet<int>();
        readonly List<Touch> touches=new List<Touch>(2);
        readonly List<RaycastResult> hits=new List<RaycastResult>();
        public int ViewIndex { get; private set; }
        public void Initialize(CharacterRig character,Camera camera)
        {
            rig=character; cameraView=camera; SetView(0); Apply(1); initialized=true;
        }
        void OnEnable() { EnhancedTouchSupport.Enable(); }
        void OnDisable() { EnhancedTouchSupport.Disable(); blockedFingers.Clear(); primaryFinger=pinchFingerA=pinchFingerB=-1; previousPinch=0; }
        public void SetView(int index)
        {
            ViewIndex=Mathf.Clamp(index,0,2);
            yaw=ViewIndex==0?30:ViewIndex==1?12:25;
            pitch=ViewIndex==0?15:ViewIndex==1?4:14;
            distance=ViewIndex==0?6.5f:ViewIndex==1?3.3f:2.5f;
            previousPinch=0; primaryFinger=pinchFingerA=pinchFingerB=-1;
        }
        bool OverUI(Vector2 position)
        {
            if(!EventSystem.current) return false;
            hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);
            return hits.Count>0;
        }
        public void Drag(Vector2 pixels,float screenHeight)
        {
            float scale=150/Mathf.Max(1,screenHeight);
            yaw=Mathf.Clamp(yaw+pixels.x*scale,-55,65);
            pitch=Mathf.Clamp(pitch-pixels.y*scale,-5,35);
        }
        public void Zoom(float ratio)
        {
            if(ratio<=0 || float.IsNaN(ratio) || float.IsInfinity(ratio)) return;
            distance=Mathf.Clamp(distance/ratio,ViewIndex==0?3.5f:1.8f,ViewIndex==0?7:4.8f);
        }
        void ReadInput()
        {
            touches.Clear();
            foreach(var touch in Touch.activeTouches)
            {
                if(touch.phase==UnityEngine.InputSystem.TouchPhase.Began)
                {
                    blockedFingers.Remove(touch.finger.index);
                    if(OverUI(touch.screenPosition)) blockedFingers.Add(touch.finger.index);
                }
                if(touch.phase==UnityEngine.InputSystem.TouchPhase.Ended || touch.phase==UnityEngine.InputSystem.TouchPhase.Canceled) { blockedFingers.Remove(touch.finger.index); continue; }
                if(!blockedFingers.Contains(touch.finger.index)) touches.Add(touch);
            }
            if(touches.Count>=2)
            {
                float separation=Vector2.Distance(touches[0].screenPosition,touches[1].screenPosition);
                bool samePair=pinchFingerA==touches[0].finger.index && pinchFingerB==touches[1].finger.index;
                if(samePair && previousPinch>10) Zoom(separation/previousPinch);
                previousPinch=separation; pinchFingerA=touches[0].finger.index; pinchFingerB=touches[1].finger.index; primaryFinger=-1;
            }
            else
            {
                previousPinch=0; pinchFingerA=pinchFingerB=-1;
                if(touches.Count==1)
                {
                    var touch=touches[0];
                    if(primaryFinger==touch.finger.index) Drag(touch.delta,Screen.height);
                    primaryFinger=touch.finger.index;
                }
                else primaryFinger=-1;
            }
            // Touch already supplies the gesture: ignore emulated mouse events on phones.
            if(!Application.isMobilePlatform && Mouse.current!=null && Touch.activeTouches.Count==0)
            {
                var mouse=Mouse.current;
                if(mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) mouseBlocked=OverUI(mouse.position.ReadValue());
                if(!mouseBlocked && (mouse.leftButton.isPressed || mouse.rightButton.isPressed)) Drag(mouse.delta.ReadValue(),Screen.height);
                if(!OverUI(mouse.position.ReadValue())) Zoom(Mathf.Exp(mouse.scroll.ReadValue().y*.001f));
            }
            if(Keyboard.current!=null)
            {
                float movement=(Keyboard.current.rightArrowKey.isPressed?1:0)-(Keyboard.current.leftArrowKey.isPressed?1:0);
                yaw=Mathf.Clamp(yaw+movement*30*Time.unscaledDeltaTime,-55,65);
            }
        }
        Vector3 Focus()
        {
            if(ViewIndex==0) return new Vector3(-.55f,1.05f,1.7f);
            return rig.transform.position+Vector3.up*(ViewIndex==1?1.05f:.48f);
        }
        void Apply(float blend)
        {
            Vector3 focus=Focus();
            Vector3 position=focus+Quaternion.Euler(pitch,yaw,0)*Vector3.back*distance;
            // All room walls are procedural and collider-free. Constrain the camera to the room.
            position.x=Mathf.Clamp(position.x,-3.5f,3.65f);
            position.y=Mathf.Clamp(position.y,.3f,2.85f);
            position.z=Mathf.Clamp(position.z,-3.1f,3.12f);
            transform.position=Vector3.Lerp(transform.position,position,blend);
            var rotation=Quaternion.LookRotation(focus-transform.position);
            transform.rotation=Quaternion.Slerp(transform.rotation,rotation,blend);
            cameraView.fieldOfView=Mathf.Lerp(cameraView.fieldOfView,ViewIndex==0?48:ViewIndex==1?40:43,blend);
        }
        void LateUpdate()
        {
            if(!initialized || !rig) return;
            ReadInput(); Apply(1-Mathf.Exp(-8*Time.unscaledDeltaTime));
        }
        public void SnapForReview() { Apply(1); }
    }
}
