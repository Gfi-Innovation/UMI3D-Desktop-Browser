﻿/*
Copyright 2019 - 2021 Inetum

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;

namespace umi3d.common.interaction
{
    /// <summary>
    /// Dto for manipulation requests.
    /// </summary>
    public class ManipulationRequestDto : InteractionRequestDto
    {

        /// <summary>
        /// The translation movement generated by the user.
        /// </summary>
        public SerializableVector3 translation;

        /// <summary>
        /// The rotation movement generated by the user.
        /// </summary>
        public SerializableVector4 rotation;

        protected override uint GetOperationId() { return UMI3DOperationKeys.ManipulationRequest; }

        public override Bytable ToBytableArray(params object[] parameters)
        {
            if (translation == null)
                translation = new SerializableVector3();
            if (rotation == null)
                rotation = new SerializableVector4();

            return base.ToBytableArray(parameters) 
                + UMI3DNetworkingHelper.Write(translation) 
                + UMI3DNetworkingHelper.Write(rotation);
        }
    }
}