﻿namespace Unosquare.FFME.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Reflection;
    using Unosquare.FFME.Primitives;

    /// <summary>
    /// Contains all the status properties of the stream being handled by the media engine.
    /// </summary>
    public class MediaStatus
    {
        #region Property Backing and Private State
        private static PropertyInfo[] Properties = null;

        private readonly MediaEngine Parent = null;
        private readonly Dictionary<string, object> CurrentState = new Dictionary<string, object>(64);

        private readonly ReadOnlyDictionary<string, string> EmptyDictionary
            = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        private AtomicBoolean m_IsSeeking = new AtomicBoolean(false);

        #endregion

        static MediaStatus()
        {
            Properties = typeof(MediaStatus).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        }

        internal MediaStatus(MediaEngine parent)
        {
            Parent = parent;
        }

        #region Media Properties

        /// <summary>
        /// Provides key-value pairs of the metadata contained in the media.
        /// Returns null when media has not been loaded.
        /// </summary>
        public ReadOnlyDictionary<string, string> Metadata => Parent.Container?.Metadata ?? EmptyDictionary;

        /// <summary>
        /// Gets the media format. Returns null when media has not been loaded.
        /// </summary>
        public string MediaFormat => Parent.Container?.MediaFormatName;

        /// <summary>
        /// Provides stream, chapter and program info of the underlying media.
        /// Returns null when no media is loaded.
        /// </summary>
        public MediaInfo MediaInfo => Parent.Container?.MediaInfo;

        /// <summary>
        /// Gets the duration of a single frame step.
        /// If there is a video component with a framerate, this propery returns the length of a frame.
        /// If there is no video component it simply returns a tenth of a second.
        /// </summary>
        public TimeSpan FrameStepDuration
        {
            get
            {
                if (IsOpen == false) { return TimeSpan.Zero; }

                if (HasVideo && VideoFrameLength > 0)
                    return TimeSpan.FromMilliseconds(VideoFrameLength * 1000);

                return TimeSpan.FromSeconds(0.1d);
            }
        }

        /// <summary>
        /// Returns whether the given media has audio.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public bool HasAudio => Parent.Container?.Components.HasAudio ?? false;

        /// <summary>
        /// Returns whether the given media has video. Only valid after the
        /// MediaOpened event has fired.
        /// </summary>
        public bool HasVideo => Parent.Container?.Components.HasVideo ?? false;

        /// <summary>
        /// Gets the video codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string VideoCodec => Parent.Container?.Components?.Video?.CodecName;

        /// <summary>
        /// Gets the video bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int VideoBitrate => Parent.Container?.Components?.Video?.Bitrate ?? 0;

        /// <summary>
        /// Returns the natural width of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int NaturalVideoWidth => Parent.Container?.Components?.Video?.FrameWidth ?? 0;

        /// <summary>
        /// Returns the natural height of the media in the video.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int NaturalVideoHeight => Parent.Container?.Components.Video?.FrameHeight ?? 0;

        /// <summary>
        /// Gets the video frame rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameRate => Parent.Container?.Components.Video?.BaseFrameRate ?? 0;

        /// <summary>
        /// Gets the duration in seconds of the video frame.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public double VideoFrameLength => 1d / (Parent.Container?.Components?.Video?.BaseFrameRate ?? 0);

        /// <summary>
        /// Gets the name of the video hardware decoder in use.
        /// Enabling hardware acceleration does not guarantee decoding will be performed in hardware.
        /// When hardware decoding of frames is in use this will return the name of the HW accelerator.
        /// Otherwise it will return an empty string.
        /// </summary>
        public string VideoHardwareDecoder { get; internal set; }

        /// <summary>
        /// Gets the audio codec.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public string AudioCodec => Parent.Container?.Components?.Audio?.CodecName;

        /// <summary>
        /// Gets the audio bitrate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitrate => Parent.Container?.Components?.Audio?.Bitrate ?? 0;

        /// <summary>
        /// Gets the audio channels count.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioChannels => Parent.Container?.Components?.Audio?.Channels ?? 0;

        /// <summary>
        /// Gets the audio sample rate.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioSampleRate => Parent.Container?.Components?.Audio?.SampleRate ?? 0;

        /// <summary>
        /// Gets the audio bits per sample.
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public int AudioBitsPerSample => Parent.Container?.Components?.Audio?.BitsPerSample ?? 0;

        /// <summary>
        /// Gets the Media's natural duration
        /// Only valid after the MediaOpened event has fired.
        /// </summary>
        public TimeSpan? NaturalDuration => Parent.Container?.MediaDuration;

        /// <summary>
        /// Returns whether the currently loaded media can be paused.
        /// This is only valid after the MediaOpened event has fired.
        /// Note that this property is computed based on wether the stream is detected to be a live stream.
        /// </summary>
        public bool CanPause => IsOpen ? !IsLiveStream : false;

        /// <summary>
        /// Returns whether the currently loaded media is live or realtime and does not have a set duration
        /// This is only valid after the MediaOpened event has fired.
        /// </summary>
        public bool IsLiveStream => IsOpen ? Parent.Container.IsLiveStream : false;

        /// <summary>
        /// Gets a value indicating whether the currently loaded media can be seeked.
        /// </summary>
        public bool IsSeekable => Parent.Container?.IsStreamSeekable ?? false;

        /// <summary>
        /// Gets a value indicating whether the media is playing.
        /// </summary>
        public bool IsPlaying => MediaState == MediaEngineState.Play;

        /// <summary>
        /// Gets a value indicating whether the media has reached its end.
        /// </summary>
        public bool HasMediaEnded { get; internal set; }

        /// <summary>
        /// Get a value indicating whether the media is buffering.
        /// </summary>
        public bool IsBuffering { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the media seeking is in progress.
        /// </summary>
        public bool IsSeeking
        {
            get => m_IsSeeking.Value == true;
            internal set => m_IsSeeking.Value = value;
        }

        /// <summary>
        /// Returns the current video SMTPE timecode if available.
        /// If not available, this property returns an empty string.
        /// </summary>
        public string VideoSmtpeTimecode { get; internal set; }

        /// <summary>
        /// Gets the guessed buffered bytes in the packet queue per second.
        /// If bitrate information is available, then it returns the bitrate converted to byte rate.
        /// Returns null if it has not been guessed.
        /// </summary>
        public ulong? GuessedByteRate { get; internal set; }

        /// <summary>
        /// Gets a value that indicates the percentage of buffering progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double BufferingProgress { get; internal set; }

        /// <summary>
        /// The packet buffer length.
        /// It is adjusted to 1 second if bitrate information is available.
        /// Otherwise, it's simply 512KB and it is guessed later on.
        /// </summary>
        public int BufferCacheLength { get; internal set; }

        /// <summary>
        /// Gets a value that indicates the percentage of download progress made.
        /// Range is from 0 to 1
        /// </summary>
        public double DownloadProgress { get; internal set; }

        /// <summary>
        /// Gets the maximum packet buffer length, according to the bitrate (if available).
        /// If it's a realtime stream it will return 30 times the buffer cache length.
        /// Otherwise, it will return  4 times of the buffer cache length.
        /// </summary>
        public int DownloadCacheLength { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the media is in the process of opening.
        /// </summary>
        public bool IsOpening { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this media element
        /// currently has an open media url.
        /// </summary>
        public bool IsOpen => (IsOpening == false) && (Parent.Container?.IsOpen ?? false);

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        public MediaEngineState MediaState { get; internal set; }

        #endregion

        /// <summary>
        /// Compiles the state into the target dictionary of property names and property values
        /// </summary>
        /// <param name="target">The target.</param>
        public void TakeSnapshotInto(Dictionary<string, object> target)
        {
            foreach (var p in Properties)
                target[p.Name] = p.GetValue(this);
        }

        /// <summary>
        /// Contrasts the specified target with the current state.
        /// It leaves the target with only the properties that are different from the current state.
        /// </summary>
        /// <param name="target">The target.</param>
        public void ContrastInto(Dictionary<string, object> target)
        {
            TakeSnapshotInto(CurrentState);
            foreach (var kvp in CurrentState)
            {
                if (target[kvp.Key] == kvp.Value)
                    target.Remove(kvp.Key);
            }
        }
    }
}