// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using SlabReconfigurationWebRole.Events;

namespace SlabReconfigurationWebRole.Messaging
{
    public class FakeMessageSender : IMessageSender
    {
        private Random randomFailure;

        public FakeMessageSender()
        {
            this.randomFailure = new Random();
        }

        public void SendMessage(string recipient, string message)
        {
            QuickStartEventSource.Log.SendingMessage(recipient, message);

            try
            {
                if (this.randomFailure.Next(10) == 0)
                {
                    throw new InvalidOperationException("Random error sending message");
                }

                QuickStartEventSource.Log.MessageSent(recipient);
            }
            catch (InvalidOperationException e)
            {
                QuickStartEventSource.Log.MessageSendingFailed(recipient, e);
                throw;
            }
        }
    }
}