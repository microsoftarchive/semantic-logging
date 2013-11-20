// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace SlabReconfigurationWebRole.Models
{
    public class MessageModel
    {
        [Required]
        [MaxLength(20)]
        public string Recipient { get; set; }

        [Required]
        [MaxLength(200)]
        public string Message { get; set; }
    }
}