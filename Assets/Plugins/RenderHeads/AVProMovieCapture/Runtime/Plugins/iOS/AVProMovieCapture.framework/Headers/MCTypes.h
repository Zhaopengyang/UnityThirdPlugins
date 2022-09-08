//
//  MCTypes.h
//  MovieCapture
//
//  Created by Morris Butler on 30/11/2020.
//  Copyright © 2020 RenderHeads. All rights reserved.
//

#import <Foundation/Foundation.h>

typedef NS_ENUM(int, MCColourSpace)
{
	MCColourSpaceUnknown = -1,
	MCColourSpaceGamma,
	MCColourSpaceLinear
};

//
typedef struct __attribute__((packed)) VideoEncoderHints {
	uint32_t      averageBitrate;
	uint32_t      maximumBitrate;				// Unsupported
	float         quality;
	uint32_t      keyframeInterval;
	bool	      allowFastStartStreamingPostProcess;
	bool	      supportTransparency;
	bool          useHardwareEncoding;			// Unsupported
	int           injectStereoPacking;			// Unsupported
	int           stereoPacking;				// Unsupported
	int           injectSphericalVideoLayout;	// Unsupported
	int           sphericalVideoLayout;			// Unsupported
	bool          enableFragmentedWriting;
	double        movieFragmentInterval;
	MCColourSpace colourSpace;
	int           sourceWidth;
	int           sourceHeight;
} VideoEncoderHints;

//
typedef struct __attribute__((packed)) ImageEncoderHints {
	float         quality;
	bool          supportTransparency;
	MCColourSpace colourSpace;
	int           sourceWidth;
	int           sourceHeight;
} ImageEncoderHints;

typedef NS_OPTIONS(int, MCMicrophoneRecordingOptions) {
	MCMicrophoneRecordingOptionsNone,
	MCMicrophoneRecordingOptionsMixWithOthers,
	MCMicrophoneRecordingOptionsDefaultToSpeaker,
};

// MARK: Ambisonics

typedef void *MCAmbisonicSourceRef;

typedef NS_ENUM(int, MCAmbisonicOrder)
{
	MCAmbisonicOrderFirst,
	MCAmbisonicOrderSecond,
	MCAmbisonicOrderThird
};

typedef NS_ENUM(int, MCAmbisonicChannelOrder)
{
	MCAmbisonicChannelOrderFuMa,
	MCAmbisonicChannelOrderACN
};

// MARK: Audio Capture

typedef NS_ENUM(int, MCAudioCaptureDeviceAuthorisationStatus)
{
	MCAudioCaptureDeviceAuthorisationStatusUnavailable = -1,
	MCAudioCaptureDeviceAuthorisationStatusNotDetermined,
	MCAudioCaptureDeviceAuthorisationStatusDenied,
	MCAudioCaptureDeviceAuthorisationStatusAuthorised,
};

typedef void (*MCRequestAudioCaptureAuthorisationCallback)(MCAudioCaptureDeviceAuthorisationStatus status);

// MARK: Photo Library

typedef NS_ENUM(int, MCPhotoLibraryAccessLevel)
{
	MCPhotoLibraryAccessLevelAddOnly,
	MCPhotoLibraryAccessLevelReadWrite
};

typedef NS_ENUM(int, MCPhotoLibraryAuthorisationStatus)
{
	MCPhotoLibraryAuthorisationStatusUnavailable = -1,
	MCPhotoLibraryAuthorisationStatusNotDetermined,
	MCPhotoLibraryAuthorisationStatusDenied,
	MCPhotoLibraryAuthorisationStatusAuthorised,
};

typedef void (*MCRequestPhotoLibraryAuthorisationCallback)(MCPhotoLibraryAuthorisationStatus status);

