#if GAMEOBJECTS_NETCODE_AVAILABLE
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.DistributedAuthority;
using UnityEngine;

namespace Unity.Services.Multiplayer
{
    /// <summary>
    /// Manages the full lifecycle of a <see cref="Netcode.NetworkManager"/>
    /// session, from setup to start to stop. Should handle all
    /// situations where the <see cref="Netcode.NetworkManager"/>
    /// is manually started or stopped separately from the session.
    /// </summary>
    class NetworkManagerSession : IDisposable
    {
        const string k_EnclosingType = nameof(NetworkManagerSession);
        const string k_NetcodeNetworkManagerType = nameof(Netcode) + "." + nameof(Netcode.NetworkManager);

        // Initial settings for any changeable state.
        bool m_IsTransportCached;
        NetworkTransport m_CachedTransport;
        NetworkRole m_NetworkRole;
        TaskCompletionSource<object> m_StartAsyncCompletion;
        TaskCompletionSource<object> m_StopAsyncCompletion;

#if GAMEOBJECTS_NETCODE_2_AVAILABLE
        bool m_IsDASettingsCached;
        bool m_CachedUseCMBService;
        NetworkTopologyTypes m_CachedTopologyType;
#endif

        /// <summary>
        /// Timeout for the <see cref="m_StartAsyncCompletion"/>
        /// and the <see cref="m_StopAsyncCompletion"/>.
        /// </summary>
        internal static TimeSpan CancellationTimeout
        {
            get;
            set;
        } = TimeSpan.FromSeconds(5d);

        internal NetworkManager NetworkManager { get; private set; }
        internal bool Disposed { get; private set; }

        public NetworkManagerSession([NotNull] NetworkManager manager, NetworkRole role)
        {
            NetworkManager = manager;
            m_NetworkRole = role;
        }

        #region Session starting and stopping

        /// <summary>
        /// Starts the <see cref="Netcode.NetworkManager"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> that will be completed
        /// once the NetworkManager finishes connecting.
        /// </returns>
        /// <exception cref="SessionException">
        /// Thrown when the <see cref="Netcode.NetworkManager"/> fails to start.
        /// </exception>
        public async Task StartAsync()
        {
            Logger.LogCallVerboseWithMessage(k_EnclosingType, $"Called for {NetworkManager.name}.");

            if (Disposed)
            {
                Logger.LogCallWarning(k_EnclosingType, "Called after dispose.");
                return;
            }

            // A started NetworkManager will always
            // either be a client or a server.
            if (NetworkManager.IsClient || NetworkManager.IsServer)
            {
                Logger.LogCallWarning(k_EnclosingType, $"{k_NetcodeNetworkManagerType} is already connected.");

                return;
            }

            if (m_StartAsyncCompletion != null)
            {
                Logger.LogCallWarning(k_EnclosingType, $"{nameof(NetworkManagerSession)} is already started.");

                await m_StartAsyncCompletion.Task;

                return;
            }

            SubscribeToRelevantCallbacks();

            // Start the NetworkManager.
            m_StartAsyncCompletion = new TaskCompletionSource<object>();

            // Register a cancellation token and link it to Application exit.
            using var cancelToken = CancellationTokenSource.CreateLinkedTokenSource(Application.exitCancellationToken);
            cancelToken.CancelAfter(CancellationTimeout);
            await using var ctr = cancelToken.Token.Register(() => m_StartAsyncCompletion.TrySetCanceled());

            try
            {
                var wasStarted = m_NetworkRole switch
                {
                    NetworkRole.Server => NetworkManager.StartServer(),
                    NetworkRole.Host => NetworkManager.StartHost(),
                    NetworkRole.Client => NetworkManager.StartClient(),
                    _ => false
                };

                if (!wasStarted)
                {
                    throw new SessionException($"Failed to start {k_NetcodeNetworkManagerType} component as {m_NetworkRole:G}.",
                        SessionError.NetworkManagerStartFailed);
                }

                await m_StartAsyncCompletion.Task;
            }
            catch (Exception e)
            {
                m_StartAsyncCompletion.TrySetCanceled();
                Dispose();
                if (e is not SessionException)
                {
                    throw new SessionException($"Failed to start {nameof(Netcode.NetworkManager)}: {e.Message}", SessionError.NetworkManagerStartFailed);
                }
                throw;
            }
        }

        /// <summary>
        /// Finalizes the state of the <see cref="m_StartAsyncCompletion"/>
        /// task. Called by callbacks once <see cref="NetworkManager"/>
        /// has started and is fully synchronized.
        /// </summary>
        private void OnStartCompleted()
        {
            if (m_StartAsyncCompletion == null)
            {
                // This should never happen. The callbacks are registered in
                // StartAsync so the completion token should always exist.
                Logger.LogCallError(k_EnclosingType, $"{k_NetcodeNetworkManagerType} has started but the task was never created.");
                Dispose();
                return;
            }

            m_StartAsyncCompletion.TrySetResult(null);
        }

        /// <summary>
        /// Stops the <see cref="Netcode.NetworkManager"/> if necessary.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> that resolves when the <see
        /// cref="Netcode.NetworkManager"/> has finished shutting down.
        /// </returns>
        public async Task StopAsync()
        {
            Logger.LogCallVerboseWithMessage(k_EnclosingType, $"Called for {NetworkManager.name}.");

            if (Disposed)
            {
                Logger.LogCallWarning(k_EnclosingType, "Called after dispose.");
                return;
            }

            // The NetworkManager won't be subscribed to the correct
            // callbacks if it was started outside StartAsync.
            if (m_StartAsyncCompletion == null)
            {
                Logger.LogCallWarning(k_EnclosingType, $"{k_NetcodeNetworkManagerType} was started outside of the session. Ensure {k_NetcodeNetworkManagerType}.{nameof(Netcode.NetworkManager.Shutdown)} is called manually.");
                return;
            }

            if (m_StopAsyncCompletion != null)
            {
                Logger.LogCallWarning(k_EnclosingType, "Called more than once.");
                await m_StopAsyncCompletion.Task;
                return;
            }

            Logger.LogCallVerboseWithMessage(k_EnclosingType,"[StopAsyncCompletion] Task created...");

            if (!NetworkManager.ShutdownInProgress)
            {
                m_StopAsyncCompletion = new TaskCompletionSource<object>();
                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(Application.exitCancellationToken);
                Logger.LogCallVerboseWithMessage(k_EnclosingType, $"[CancellationTokenSource] NetworkManager shutdown timeout period: {CancellationTimeout}.");
                tokenSource.CancelAfter(CancellationTimeout);
                await using var ctr = tokenSource.Token.Register(() =>
                {
                    if (Application.exitCancellationToken.IsCancellationRequested)
                    {
                        Logger.LogCallVerboseWithMessage(k_EnclosingType, "Exiting early because application is shutting down.");
                        return;
                    }

                    Logger.LogCallVerboseWithMessage(k_EnclosingType,"Exiting early because we timed out.");

                    // Assume the shutdown succeeded and continue.
                    m_StopAsyncCompletion.TrySetResult(null);
                });

                // Do the shutdown.
                NetworkManager.Shutdown();
                await m_StopAsyncCompletion.Task;
                Logger.LogCallVerboseWithMessage(k_EnclosingType, "Session stopped. Disposing...");
            }
            else
            {
                Logger.LogCallWarning(k_EnclosingType, $"{k_NetcodeNetworkManagerType} is already shutting down. Do not call {k_NetcodeNetworkManagerType}.{nameof(Netcode.NetworkManager.Shutdown)} when using a session.");
            }

            Dispose();
        }

        /// <summary>
        /// Finalizes the state of the <see cref="m_StopAsyncCompletion"/>
        /// task. Called by callbacks once <see
        /// cref="NetworkManager"/> has finished shutting down.
        /// </summary>
        private async Task OnStopCompleted()
        {
            if (m_StopAsyncCompletion == null)
            {
                Logger.LogCallWarning(k_EnclosingType, $"{k_NetcodeNetworkManagerType} has been shutdown outside of a session. Do not call {k_NetcodeNetworkManagerType}.{nameof(Netcode.NetworkManager.Shutdown)} when using a session.");
                return;
            }

            // Wait for the current frame to finish processing
            // (allows the shutdown to completely finish).
#if UNITY_6000_0_OR_NEWER
            await Awaitable.EndOfFrameAsync();
#else
            await Compatibility.WaitForEndOfFrameAsync(NetworkManager);
#endif
            m_StopAsyncCompletion.TrySetResult(null);
        }
        #endregion

        #region Getters and setters
        public void SetNetworkRole(NetworkRole newRole)
        {
            m_NetworkRole = newRole;
        }

        public UnityTransport GetUnityTransport()
        {
            return NetworkManager.NetworkConfig.NetworkTransport as UnityTransport;
        }

#if GAMEOBJECTS_NETCODE_2_AVAILABLE
        public DistributedAuthorityTransport GetDistributedAuthorityTransport()
        {
            return NetworkManager.NetworkConfig.NetworkTransport as DistributedAuthorityTransport;
        }
#endif
        public void SetTransport(UnityTransport transport)
        {
            if (Disposed)
            {
                return;
            }

            // If the transport was already cached,
            // ensure we don't override the cache.
            if (!m_IsTransportCached)
            {
                m_IsTransportCached = true;
                m_CachedTransport = NetworkManager.NetworkConfig.NetworkTransport;
            }

            NetworkManager.NetworkConfig.NetworkTransport = transport;
        }

#if GAMEOBJECTS_NETCODE_2_AVAILABLE
        public void ConfigureForDistributedAuthority()
        {
            if (Disposed)
            {
                return;
            }

            // If the settings were already cached,
            // ensure we don't override the cache.
            if (!m_IsDASettingsCached)
            {
                m_IsDASettingsCached = true;
                m_CachedUseCMBService = NetworkManager.NetworkConfig.UseCMBService;
                m_CachedTopologyType = NetworkManager.NetworkConfig.NetworkTopology;
            }

            NetworkManager.NetworkConfig.UseCMBService = true;
            NetworkManager.NetworkConfig.NetworkTopology = NetworkTopologyTypes.DistributedAuthority;
        }
#endif
        #endregion

        #region Callbacks
        private void SubscribeToRelevantCallbacks()
        {
            switch (m_NetworkRole)
            {
                // Server doesn't get any client events 100% ready to receive
                // on OnServerStarted Finished shutting down OnServerStopped.
                case NetworkRole.Server:
                    NetworkManager.OnServerStarted += OnServerStarted;
                    NetworkManager.OnServerStopped += OnManagerStopped;
                    break;
                // Host is both a server and a client, it wants to subscribe to
                // the last events in the invocation order. OnConnectionEvent
                // is the client connection event and is the last to be
                // invoked OnServerStopped is invoked after OnClientStopped.
                case NetworkRole.Host:
                    NetworkManager.OnConnectionEvent += OnConnectionEvent;
                    NetworkManager.OnServerStopped += OnManagerStopped;
                    break;
                // Client only has the client specific callbacks.
                case NetworkRole.Client:
                    NetworkManager.OnConnectionEvent += OnConnectionEvent;
                    NetworkManager.OnClientStopped += OnManagerStopped;
                    break;
            }
        }

        private void DisposeCallbacks()
        {
            NetworkManager.OnServerStarted -= OnServerStarted;
            NetworkManager.OnConnectionEvent -= OnConnectionEvent;
            NetworkManager.OnClientStopped -= OnManagerStopped;
            NetworkManager.OnServerStopped -= OnManagerStopped;
        }

        /// <summary>
        /// Callback that will be called whenever there's a <see
        /// cref="ConnectionEvent"/> on the NetworkManager..
        /// </summary>
        /// <param name="eventManager">
        /// The <see cref="Netcode.NetworkManager"/> the event was called on.
        /// </param>
        /// <param name="eventData">
        /// Information about the connection event.
        /// </param>
        private void OnConnectionEvent(NetworkManager eventManager, ConnectionEventData eventData)
        {
            Logger.LogCallVerbose(k_EnclosingType);

            if (eventData.EventType == ConnectionEvent.ClientConnected)
            {
                if (NetworkManager.LocalClientId != eventManager.LocalClientId ||
                    NetworkManager.LocalClientId != eventData.ClientId)
                {
                    Logger.LogCallError(k_EnclosingType, $"[{nameof(StartAsync)}] received unexpected {nameof(ConnectionEvent)} for Client-{eventData.ClientId}.");
                    return;
                }

                OnStartCompleted();
                eventManager.OnConnectionEvent -= OnConnectionEvent;
            }
        }

        private void OnServerStarted()
        {
            Logger.LogCallVerbose(k_EnclosingType);

            OnStartCompleted();
            NetworkManager.OnServerStarted -= OnServerStarted;
        }

        /// <summary>
        /// Callback that will be invoked whenever the <see
        /// cref="NetworkManager"/> is fully shut down.
        /// </summary>
        private async void OnManagerStopped(bool _)
        {
            if (Disposed)
            {
                return;
            }
            Logger.LogCallVerbose(k_EnclosingType);
            await OnStopCompleted();

            // Call dispose to clean up this NetworkManager session.
            Dispose();
        }
        #endregion

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }

            Logger.LogCallVerbose(k_EnclosingType);

            DisposeCallbacks();

            if (m_IsTransportCached)
            {
                NetworkManager.NetworkConfig.NetworkTransport = m_CachedTransport;
            }

#if GAMEOBJECTS_NETCODE_2_AVAILABLE
            if (m_IsDASettingsCached)
            {
                NetworkManager.NetworkConfig.UseCMBService = m_CachedUseCMBService;
                NetworkManager.NetworkConfig.NetworkTopology = m_CachedTopologyType;
            }
#endif
            if (m_StartAsyncCompletion != null)
            {
                if (!m_StartAsyncCompletion.Task.IsCompleted)
                {
                    m_StartAsyncCompletion.TrySetCanceled();
                }

                m_StartAsyncCompletion = null;
            }

            if (m_StopAsyncCompletion != null)
            {
                if (!m_StopAsyncCompletion.Task.IsCompleted)
                {
                    m_StopAsyncCompletion.TrySetCanceled();
                }

                m_StopAsyncCompletion = null;
            }

            Disposed = true;
        }
    }
}
#endif
