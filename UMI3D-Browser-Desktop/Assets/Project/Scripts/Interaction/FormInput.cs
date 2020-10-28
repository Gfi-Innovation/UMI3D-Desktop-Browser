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
using umi3d.cdk;
using umi3d.cdk.interaction;
using umi3d.common.interaction;
using umi3d.common.userCapture;

[System.Serializable]
public class FormInput : AbstractUMI3DInput
{
    /// <summary>
    /// Associtated interaction (if any).
    /// </summary>
    public FormDto associatedInteraction { get; protected set; }
    /// <summary>
    /// Avatar bone linked to this input.
    /// </summary>
    public string bone = BoneType.RightHand;

    string toolId;
    string hoveredObjectId;

    protected BoneDto boneDto;
    bool risingEdgeEventSent;

    HoldableButtonMenuItem menuItem;

    public override void Associate(AbstractInteractionDto interaction, string toolId, string hoveredObjectId)
    {
        if (associatedInteraction != null)
        {
            throw new System.Exception("This input is already binded to a interaction ! (" + associatedInteraction + ")");
        }

        if (IsCompatibleWith(interaction))
        {
            this.hoveredObjectId = hoveredObjectId;
            this.toolId = toolId;
            associatedInteraction = interaction as FormDto;
            menuItem = new HoldableButtonMenuItem
            {
                Name = associatedInteraction.name,
                Holdable = false
            };
            menuItem.Subscribe(Pressed);
            if (CircleMenu.Exists)
            {
                CircleMenu.Instance.MenuDisplayManager.menu.Add(menuItem);
            }
        }
        else
        {
            throw new System.Exception("Trying to associate an uncompatible interaction !");
        }
    }

    public override void Associate(ManipulationDto manipulation, DofGroupEnum dofs, string toolId, string hoveredObjectId)
    {
        throw new System.Exception("This input is can not be associated with a manipulation");
    }

    public override AbstractInteractionDto CurrentInteraction()
    {
        return associatedInteraction;
    }

    public override void Dissociate()
    {
        associatedInteraction = null;
        if (CircleMenu.Exists && menuItem != null)
        {
            CircleMenu.Instance.MenuDisplayManager.menu.Remove(menuItem);
        }
        menuItem.UnSubscribe(Pressed);
        menuItem = null;
    }

    public override bool IsAvailable()
    {
        return associatedInteraction == null;
    }

    public override bool IsCompatibleWith(AbstractInteractionDto interaction)
    {
        return interaction is FormDto;
    }

    void Pressed(bool down)
    {
        if (boneDto == null)
            boneDto = AvatarTempo.getBoneID();
        if (down)
        {
            onInputDown.Invoke();
          
            var formAnswer = new FormAnswer
            {
                boneType = boneDto.boneType,
                id = associatedInteraction.id,
                toolId = this.toolId,
                form = associatedInteraction,
                hoveredObjectId = hoveredObjectId
            };
            UMI3DClientServer.Send(formAnswer, true);
        }
    }
}
