// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace SlabReconfigurationWebRole.Messaging
{
    public interface IMessageSender
    {
        void SendMessage(string recipient, string message);
    }
}