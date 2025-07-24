using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;

using UnityEngine;

using Vector3 = UnityEngine.Vector3;

public class UILerpElement : MonoBehaviour
{
    [SerializeField] private bool useLocalSpace = false;
    [SerializeField] private Vector3 startPos;
    [SerializeField] private Vector3 endPos;
    private bool lerping = false;

    [SerializeField] private float lerpSpeed;
    
    [ContextMenu("Start Lerp")]
    public void StartLerp() {
        
        lerping = true;
        enabled = true;
        
        if (useLocalSpace) {
            transform.localPosition = startPos;
        } else {
            transform.position = startPos;
        }
    }

    public void SetLerpSpeed(float newSpeed) {
        lerpSpeed = newSpeed;
    }
    
    private void Update() {
        if (lerping) {
            if (useLocalSpace) {
                transform.localPosition = Vector3.Lerp(transform.localPosition, endPos, lerpSpeed * Time.deltaTime);
            } else {
                transform.position = Vector3.Lerp(transform.position, endPos, lerpSpeed * Time.deltaTime);
            }
            

            float dist = useLocalSpace ? Vector3.Distance(transform.localPosition, endPos) : Vector3.Distance(transform.position, endPos);
            if (dist <= 0.05f) {
                enabled = false;
                lerping = false;

                if (useLocalSpace) {
                    transform.localPosition = endPos;
                } else {
                    transform.position = endPos;
                }
            }
        }
        
    }
}
