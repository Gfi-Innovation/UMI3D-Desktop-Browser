﻿/*
Copyright 2019 Gfi Informatique

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
using BrowserDesktop.Menu;
using umi3d.cdk.collaboration;
using umi3d.cdk.menu;
using umi3d.common.interaction;

namespace BrowserDesktop.Parameters
{
    public abstract class AbstractRangeParameterInput<InputMenuItem, ParameterType, ValueType> : AbstractParameterInput<InputMenuItem, ParameterType, ValueType>
    where ValueType : System.IComparable
    where InputMenuItem : AbstractRangeInputMenuItem<ValueType>, new()
    where ParameterType : AbstractRangeParameterDto<ValueType>
    {

        public override void Associate(AbstractInteractionDto interaction, string toolId)
        {
            if (currentInteraction != null)
            {
                throw new System.Exception("This input is already associated to another interaction (" + currentInteraction + ")");
            }

            if (interaction is ParameterType)
            {
                ParameterType param = interaction as ParameterType;
                menuItem = new InputMenuItem()
                {
                    dto = interaction as ParameterType,
                    min = param.Min,
                    max = param.Max,
                    Name = param.name,
                    value = param.value,
                    increment = param.Increment
                };

                callback = x =>
                {
                    if ((x.CompareTo(param.Min) >= 0) && (x.CompareTo(param.Max) <= 0))
                    {
                        var dto = interaction as ParameterType;
                        dto.value = x;
                        var pararmeterDto = new ParameterSettingRequestDto()
                        {
                            id = currentInteraction.id,
                            toolId = toolId,
                            parameter = dto,
                        };
                        UMI3DCollaborationClientServer.Send(pararmeterDto, true);
                    }
                };

                menuItem.NotifyValueChange(param.value);
                menuItem.Subscribe(callback);
                if (CircleMenu.Exists)
                    CircleMenu.Instance.MenuDisplayManager.menu.Add(menuItem);
                currentInteraction = interaction;
            }
            else
            {
                throw new System.Exception("Incompatible interaction");
            }
        }
    }
}