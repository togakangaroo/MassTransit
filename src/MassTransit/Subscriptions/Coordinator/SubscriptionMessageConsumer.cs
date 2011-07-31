// Copyright 2007-2011 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.Subscriptions.Coordinator
{
	using System;
	using System.Collections.Generic;
	using Messages;
	using Services.Subscriptions.Messages;
	using log4net;

	public class SubscriptionMessageConsumer :
		Consumes<AddSubscriptionClient>.Context,
		Consumes<RemoveSubscriptionClient>.Context,
		Consumes<SubscriptionRefresh>.Context,
		Consumes<AddSubscription>.Context,
		Consumes<RemoveSubscription>.Context
	{
		static readonly ILog _log = LogManager.GetLogger(typeof (SubscriptionMessageConsumer));
		readonly BusSubscriptionCoordinator _coordinator;
		readonly HashSet<Uri> _ignoredSourceAddresses;
		readonly string _network;

		public SubscriptionMessageConsumer(BusSubscriptionCoordinator coordinator, string network,
		                                   params Uri[] ignoredSourceAddresses)
		{
			_coordinator = coordinator;
			_network = network;
			_ignoredSourceAddresses = new HashSet<Uri>(ignoredSourceAddresses);
		}

		public void Consume(IConsumeContext<AddSubscription> context)
		{
			if (DiscardMessage(context))
				return;

			_coordinator.Send(new AddPeerSubscriptionMessage
				{
					PeerId = context.Message.Subscription.ClientId,
					EndpointUri = context.Message.Subscription.EndpointUri,
					MessageName = context.Message.Subscription.MessageName,
					MessageNumber = context.Message.Subscription.SequenceNumber,
					SubscriptionId = context.Message.Subscription.SubscriptionId,
				});
		}

		public void Consume(IConsumeContext<AddSubscriptionClient> context)
		{
			if (DiscardMessage(context))
				return;

			_coordinator.Send(new AddPeerMessage
				{
					PeerId = context.Message.CorrelationId,
					PeerUri = context.Message.ControlUri,
					Timestamp = DateTime.UtcNow.Ticks,
				});
		}

		public void Consume(IConsumeContext<RemoveSubscription> context)
		{
			if (DiscardMessage(context))
				return;

			_coordinator.Send(new RemovePeerSubscriptionMessage
				{
					PeerId = context.Message.Subscription.ClientId,
					EndpointUri = context.Message.Subscription.EndpointUri,
					MessageName = context.Message.Subscription.MessageName,
					MessageNumber = context.Message.Subscription.SequenceNumber,
					SubscriptionId = context.Message.Subscription.SubscriptionId,
				});
		}

		public void Consume(IConsumeContext<RemoveSubscriptionClient> context)
		{
			if (DiscardMessage(context))
				return;

			_coordinator.Send(new RemovePeerMessage
				{
					PeerId = context.Message.CorrelationId,
					PeerUri = context.Message.ControlUri,
					Timestamp = DateTime.UtcNow.Ticks,
				});
		}

		public void Consume(IConsumeContext<SubscriptionRefresh> context)
		{
			if (DiscardMessage(context))
				return;

			foreach (SubscriptionInformation subscription in context.Message.Subscriptions)
			{
				// TODO do we trust subscriptions that are third-party (sent to us from systems that are not the
				// system containing the actual subscription)

				_coordinator.Send(new AddPeerSubscriptionMessage
					{
						PeerId = subscription.ClientId,
						EndpointUri = subscription.EndpointUri,
						MessageName = subscription.MessageName,
						MessageNumber = subscription.SequenceNumber,
						SubscriptionId = subscription.SubscriptionId,
					});
			}
		}

		bool DiscardMessage<T>(IConsumeContext<T> context)
			where T : class
		{
			if (_ignoredSourceAddresses.Contains(context.SourceAddress))
			{
				_log.Debug("Ignoring subscription because its source address equals the busses address");
				return true;
			}

			if (!string.Equals(context.Network, _network))
			{
				_log.DebugFormat("Ignoring subscription because the network '{0}' != ours '{1}1", context.Network, _network);
				return true;
			}

			return false;
		}
	}
}