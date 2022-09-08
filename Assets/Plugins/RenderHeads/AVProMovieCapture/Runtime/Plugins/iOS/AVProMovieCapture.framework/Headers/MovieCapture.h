//
//  MovieCapture.h
//  MovieCapture
//
//  Created by Morris Butler on 13/05/2019.
//  Copyright © 2019 RenderHeads. All rights reserved.
//

#include "MCTypes.h"

#ifdef __cplusplus
extern "C" {
#endif

NS_ASSUME_NONNULL_BEGIN

void *AVPMC_GetRenderEventFunc(void);
void *AVPMC_GetFreeResourcesEventFunc(void);
bool  AVPMC_Init(void);
void  AVPMC_Deinit(void);
void  AVPMC_SetMicrophoneRecordingHint(bool enabled, MCMicrophoneRecordingOptions options);
bool  AVPMC_IsTrialVersion(void);
int   AVPMC_GetVideoCodecMediaApi(int index);
int   AVPMC_GetVideoCodecCount(void);
bool  AVPMC_IsConfigureVideoCodecSupported(int index);
void  AVPMC_ConfigureVideoCodec(int index);
int   AVPMC_GetAudioCodecMediaApi(int index);
int   AVPMC_GetAudioCodecCount(void);
bool  AVPMC_IsConfigureAudioCodecSupported(int index);
void  AVPMC_ConfigureAudioCodec(int index);
int   AVPMC_GetAudioInputDeviceCount(void);
int   AVPMC_CreateRecorderVideo(const unichar *filename, uint width, uint height, float frameRate, int format, bool isRealTime, bool isTopDown, int videoCodecIndex, int audioSource, int audioSampleRate, int audioChannelCount, int audioInputDeviceIndex, int audioCodecIndex, bool forceGpuFlush, VideoEncoderHints *hints);
int   AVPMC_CreateRecorderImages(const unichar *filename, uint width, uint height, float frameRate, int format, bool isRealTime, bool isTopDown, int imageFormatType, bool forceGpuFlush, int startFrame, ImageEncoderHints *hints);
int   AVPMC_CreateRecorderPipe(const unichar *filename, uint width, uint height, float frameRate, int format, bool isTopDown, bool supportAlpha, bool forceGpuFlush);
bool  AVPMC_Start(int handle);
bool  AVPMC_IsNewFrameDue(int handle);
void  AVPMC_EncodeFrame(int handle, void *data);
void  AVPMC_EncodeAudio(int handle, void *data, uint length);
void  AVPMC_EncodeFrameWithAudio(int handle, void *videoData, void *audioData, uint audioLength);
void  AVPMC_Pause(int handle);
void  AVPMC_Stop(int handle, bool skipPendingFrames);
bool  AVPMC_IsFileWritingComplete(int handle);
int   AVPMC_SetEncodedFrameLimit(int handle, uint limit);
void  AVPMC_SetTexturePointer(int handle, void *texture);
void  AVPMC_FreeRecorder(int handle);
uint  AVPMC_GetNumDroppedFrames(int handle);
uint  AVPMC_GetNumDroppedEncoderFrames(int handle);
uint  AVPMC_GetNumEncodedFrames(int handle);
uint  AVPMC_GetEncodedSeconds(int handle);
uint  AVPMC_GetFileSize(int handle);
void *AVPMC_GetPluginVersion(void);
bool  AVPMC_GetVideoCodecName(int index, unichar *name, int nameBufferLength);
bool  AVPMC_GetAudioCodecName(int index, unichar *name, int nameBufferLength);
bool  AVPMC_GetAudioInputDeviceName(int index, unichar *name, int nameBufferLength);
bool  AVPMC_GetContainerFileExtensions(int videoCodecIndex, int audioCodecIndex, unichar *extensions, int extensionsLength);
void  AVPMC_SetLogFunction(void *logFunction);
void  AVPMC_SetErrorHandler(int index, void *errorHandler);

// Audio Capture
MCAudioCaptureDeviceAuthorisationStatus AVPMC_AudioCaptureDeviceAuthorisationStatus(void);
void  AVPMC_RequestAudioCaptureDeviceAuthorisation(MCRequestAudioCaptureAuthorisationCallback callback);

// Photo Library
MCPhotoLibraryAuthorisationStatus AVPMC_PhotoLibraryAuthorisationStatus(MCPhotoLibraryAccessLevel level);
void  AVPMC_RequestPhotoLibraryAuthorisation(MCPhotoLibraryAccessLevel level, MCRequestPhotoLibraryAuthorisationCallback callback);

// Ambisonic support
MCAmbisonicSourceRef _Nullable AVPMC_AddAmbisonicSourceInstance(int maxCoefficients);
void  AVPMC_RemoveAmbisonicSourceInstance(MCAmbisonicSourceRef source);
void  AVPMC_UpdateAmbisonicWeights(MCAmbisonicSourceRef source, float azimuth, float elevation, MCAmbisonicOrder ambisonicOrder, MCAmbisonicChannelOrder channelOrder, float *weights);
void  AVPMC_EncodeMonoToAmbisonic(MCAmbisonicSourceRef source, float *inSamples, int inOffset, int inCount, int numChannels, void *outSamples, int outOffset, int outCount, MCAmbisonicOrder ambisonicOrder);

void AVPMC_UnityRegisterRenderingPlugin(void *unityRegisterRenderingPluginFunction);

NS_ASSUME_NONNULL_END

#if __cplusplus
}
#endif
