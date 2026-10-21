namespace nvidiaProfileInspector.Common.Helper
{
    using nvidiaProfileInspector.Native.WINAPI;
    using System;
    using System.Runtime.InteropServices;
    using System.Windows;

    /// <summary>
    /// Validates persisted window geometry against the current desktop layout. A stale position
    /// saved on a monitor that no longer exists (or on a display with a different DPI) must never
    /// leave the window fully outside the visible desktop, because the window would then only be
    /// reachable through its taskbar button.
    /// </summary>
    internal static class WindowPlacementHelper
    {
        private const double DefaultWidth = 1360.0;
        private const double DefaultHeight = 750.0;
        private const double MinimumVisibleWidth = 200.0;
        private const double MinimumVisibleHeight = 100.0;

        /// <summary>
        /// Clamps the desired bounds to the current virtual desktop. When the desired position is
        /// unusable the bounds are centered on the primary work area instead.
        /// </summary>
        /// <returns>False when the desktop layout is unavailable and the bounds were left untouched.</returns>
        internal static bool TryNormalizeBounds(Rect desiredBounds, Size minimumSize, out Rect normalizedBounds)
        {
            if (desiredBounds.IsEmpty)
                desiredBounds = new Rect(0, 0, DefaultWidth, DefaultHeight);

            normalizedBounds = desiredBounds;

            var virtualScreen = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

            if (virtualScreen.Width <= 0 || virtualScreen.Height <= 0)
                return false;

            double minimumWidth = Math.Max(0, minimumSize.Width);
            double minimumHeight = Math.Max(0, minimumSize.Height);
            double width = ClampSize(desiredBounds.Width, minimumWidth, virtualScreen.Width, DefaultWidth);
            double height = ClampSize(desiredBounds.Height, minimumHeight, virtualScreen.Height, DefaultHeight);

            var candidate = new Rect(desiredBounds.Left, desiredBounds.Top, width, height);
            if (!IsFinite(candidate.Left) || !IsFinite(candidate.Top) || !HasVisibleArea(candidate, virtualScreen))
            {
                var workArea = SystemParameters.WorkArea;
                if (workArea.Width <= 0 || workArea.Height <= 0)
                    workArea = virtualScreen;

                normalizedBounds = new Rect(
                    Math.Round(workArea.Left + Math.Max(0, (workArea.Width - width) / 2)),
                    Math.Round(workArea.Top + Math.Max(0, (workArea.Height - height) / 2)),
                    width,
                    height);
                return true;
            }

            double maxLeft = Math.Max(virtualScreen.Left, virtualScreen.Right - width);
            double maxTop = Math.Max(virtualScreen.Top, virtualScreen.Bottom - height);
            normalizedBounds = new Rect(
                Math.Round(Math.Min(Math.Max(candidate.Left, virtualScreen.Left), maxLeft)),
                Math.Round(Math.Min(Math.Max(candidate.Top, virtualScreen.Top), maxTop)),
                width,
                height);
            return true;
        }

        /// <summary>
        /// Moves a live window handle back into the primary work area when Windows reports that it
        /// does not intersect any monitor. Used for windows owned by an already running instance.
        /// </summary>
        internal static bool EnsureHandleOnScreen(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return false;

            try
            {
                // A minimized window has a bogus rect; bringAppToFront restores it to its real position.
                if (MessageHelper.IsIconic(handle))
                    return false;

                if (MessageHelper.MonitorFromWindow(handle, MessageHelper.MONITOR_DEFAULTTONULL) != IntPtr.Zero)
                    return false;

                if (!MessageHelper.GetWindowRect(handle, out var windowRect))
                    return false;

                var workArea = GetPrimaryWorkArea();
                int width = Math.Min(Math.Max(1, windowRect.Right - windowRect.Left), Math.Max(1, workArea.Right - workArea.Left));
                int height = Math.Min(Math.Max(1, windowRect.Bottom - windowRect.Top), Math.Max(1, workArea.Bottom - workArea.Top));
                int left = workArea.Left + 40;
                int top = workArea.Top + 40;

                return MessageHelper.SetWindowPos(
                    handle,
                    IntPtr.Zero,
                    left,
                    top,
                    width,
                    height,
                    MessageHelper.SWP_NOZORDER | MessageHelper.SWP_SHOWWINDOW);
            }
            catch
            {
                return false;
            }
        }

        private static MessageHelper.RECT GetPrimaryWorkArea()
        {
            var monitor = MessageHelper.MonitorFromWindow(IntPtr.Zero, MessageHelper.MONITOR_DEFAULTTOPRIMARY);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MessageHelper.MONITORINFO { cbSize = Marshal.SizeOf<MessageHelper.MONITORINFO>() };
                if (MessageHelper.GetMonitorInfo(monitor, ref monitorInfo))
                    return monitorInfo.rcWork;
            }

            return new MessageHelper.RECT { Left = 0, Top = 0, Right = 1024, Bottom = 768 };
        }

        private static bool HasVisibleArea(Rect candidate, Rect virtualScreen)
        {
            var intersection = Rect.Intersect(candidate, virtualScreen);
            if (intersection.IsEmpty)
                return false;

            return intersection.Width >= Math.Min(MinimumVisibleWidth, candidate.Width)
                && intersection.Height >= Math.Min(MinimumVisibleHeight, candidate.Height);
        }

        private static double ClampSize(double value, double minimum, double maximum, double fallback)
        {
            if (!IsFinite(value) || value <= 0)
                value = fallback;

            value = Math.Min(value, maximum);
            if (minimum > 0)
                value = Math.Max(value, Math.Min(minimum, maximum));

            return value > 0 ? value : fallback;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
