﻿/*
 * Copyright (c) 2018 ETH Zürich, Educational Development and Technology (LET)
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.ServiceModel;
using SafeExamBrowser.Contracts.Communication;
using SafeExamBrowser.Contracts.Communication.Messages;
using SafeExamBrowser.Contracts.Communication.Responses;
using SafeExamBrowser.Contracts.Logging;
using SafeExamBrowser.Core.Communication.Responses;

namespace SafeExamBrowser.Core.Communication
{
	[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Single)]
	public abstract class BaseHost : ICommunication, ICommunicationHost
	{
		private string address;
		private ILogger logger;
		private ServiceHost host;

		protected Guid? CommunicationToken { get; private set; }
		protected ILogger Logger { get; private set; }

		public bool IsRunning
		{
			get { return host?.State == CommunicationState.Opened; }
		}

		public BaseHost(string address, ILogger logger)
		{
			this.address = address;
			this.logger = logger;
		}

		protected abstract bool OnConnect(Guid? token);
		protected abstract void OnDisconnect();
		protected abstract IResponse OnReceive(IMessage message);
		protected abstract IResponse OnReceive(Message message);

		public IConnectionResponse Connect(Guid? token = null)
		{
			logger.Debug($"Received connection request with authentication token '{token}'.");

			var response = new ConnectionResponse();
			var connected = OnConnect(token);

			if (connected)
			{
				response.CommunicationToken = CommunicationToken = Guid.NewGuid();
				response.ConnectionEstablished = true;
			}

			logger.Debug($"{(connected ? "Accepted" : "Denied")} connection request.");

			return response;
		}

		public IDisconnectionResponse Disconnect(IDisconnectionMessage message)
		{
			var response = new DisconnectionResponse();

			// TODO: Compare with ToString in BaseProxy - needed?
			logger.Debug($"Received disconnection request with message '{message}'.");

			if (IsAuthorized(message?.CommunicationToken))
			{
				OnDisconnect();

				CommunicationToken = null;
				response.ConnectionTerminated = true;
			}

			return response;
		}

		public IResponse Send(IMessage message)
		{
			IResponse response = null;

			logger.Debug($"Received message '{message}'.");

			if (IsAuthorized(message?.CommunicationToken))
			{
				if (message is ISimpleMessage)
				{
					response = OnReceive((message as ISimpleMessage).Purport);
				}
				else
				{
					response = OnReceive(message);
				}
			}

			logger.Debug($"Sending response '{response}'.");

			return response;
		}

		public void Start()
		{
			host = new ServiceHost(this);
			host.AddServiceEndpoint(typeof(ICommunication), new NetNamedPipeBinding(NetNamedPipeSecurityMode.Transport), address);
			host.Closed += Host_Closed;
			host.Closing += Host_Closing;
			host.Faulted += Host_Faulted;
			host.Opened += Host_Opened;
			host.Opening += Host_Opening;
			host.UnknownMessageReceived += Host_UnknownMessageReceived;
			host.Open();

			logger.Debug($"Successfully started communication host for endpoint '{address}'.");
		}

		public void Stop()
		{
			host?.Close();
			logger.Debug($"Terminated communication host for endpoint '{address}'.");
		}

		private bool IsAuthorized(Guid? token)
		{
			return CommunicationToken == token;
		}

		private void Host_Closed(object sender, EventArgs e)
		{
			logger.Debug("Communication host has been closed.");
		}

		private void Host_Closing(object sender, EventArgs e)
		{
			logger.Debug("Communication host is closing...");
		}

		private void Host_Faulted(object sender, EventArgs e)
		{
			logger.Debug("Communication host has faulted!");
		}

		private void Host_Opened(object sender, EventArgs e)
		{
			logger.Debug("Communication host has been opened.");
		}

		private void Host_Opening(object sender, EventArgs e)
		{
			logger.Debug("Communication host is opening...");
		}

		private void Host_UnknownMessageReceived(object sender, UnknownMessageReceivedEventArgs e)
		{
			logger.Debug($"Communication host has received an unknown message: {e?.Message}.");
		}
	}
}
