using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture.Editor
{
    public class EditorCoroutine : MonoBehaviour
    {
        public void RunCoroutine(IEnumerator routine)
        {
            StartCoroutine(routine);
        }
    }
}