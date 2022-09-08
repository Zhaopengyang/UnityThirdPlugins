//
//  AVProMovieCapture.m
//  AVPro Movie Capture
//
//  Created by Morris Butler on 13/10/2021.
//  Copyright © 2021 RenderHeads. All rights reserved.
//

extern void AVPMC_UnityRegisterRenderingPlugin(void *unityRegisterRenderingPluginFunction);

void AVPMC_PluginBootstrap(void)
{
	AVPMC_UnityRegisterRenderingPlugin(UnityRegisterRenderingPluginV5);
}
