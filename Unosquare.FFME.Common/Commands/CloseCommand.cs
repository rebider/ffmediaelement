﻿namespace Unosquare.FFME.Commands
{
    using Core;
    using Shared;
    using System;
    using System.Text;

    /// <summary>
    /// Implements the logic to close a media stream.
    /// </summary>
    /// <seealso cref="MediaCommand" />
    internal sealed class CloseCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CloseCommand" /> class.
        /// </summary>
        /// <param name="manager">The media element.</param>
        public CloseCommand(MediaCommandManager manager)
            : base(manager, MediaCommandType.Close)
        {
            // placeholder
        }

        /// <summary>
        /// Executes this command.
        /// </summary>
        internal override void ExecuteInternal()
        {
            var m = Manager.MediaCore;

            if (m.IsDisposed || m.State.IsOpen == false || m.State.IsOpening) return;

            m.Log(MediaLogMessageType.Debug, $"{nameof(CloseCommand)}: Entered");
            m.StopWorkers();

            // Dispose the container
            if (m.Container != null)
            {
                m.Container.Dispose();
                m.Container = null;
            }

            // Dispose the Blocks for all components
            foreach (var kvp in m.Blocks) kvp.Value.Dispose();
            m.Blocks.Clear();

            // Clear the render times
            m.LastRenderTime.Clear();

            // Update notification properties
            m.State.ResetMediaProperties();
            m.State.InitializeBufferingProperties();
            m.State.UpdateMediaState(PlaybackStatus.Close, TimeSpan.Zero);
            m.State.Source = null;
            m.SendOnMediaClosed();

            if (MediaEngine.Platform.IsInDebugMode)
            {
                if (RC.Current.InstancesByLocation.Count > 0)
                {
                    var builder = new StringBuilder();
                    builder.AppendLine("Unmanaged references were left alive. This is an indication that there is a memory leak.");
                    foreach (var kvp in RC.Current.InstancesByLocation)
                        builder.AppendLine($"    {kvp.Key,30}: {kvp.Value}");

                    m.Log(MediaLogMessageType.Error, builder.ToString());
                }
            }

            m.Log(MediaLogMessageType.Debug, $"{nameof(CloseCommand)}: Completed");
        }
    }
}
