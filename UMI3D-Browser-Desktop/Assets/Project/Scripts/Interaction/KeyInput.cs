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
using BrowserDesktop.Controller;
using BrowserDesktop.Menu;
using umi3d.cdk.collaboration;
using umi3d.cdk.interaction;
using umi3d.common.interaction;
using umi3d.common.userCapture;
using UnityEngine;

namespace BrowserDesktop.Interaction
{
    [System.Serializable]
    public class KeyInput : AbstractUMI3DInput
    {
        /// <summary>
        /// Button to activate this input.
        /// </summary>
        public InputLayoutManager.Input activationButton;
        private KeyCode LastFrameButton;

        /// <summary>
        /// Avatar bone linked to this input.
        /// </summary>
        public string bone = BoneType.RightHand;

        protected BoneDto boneDto;

        /// <summary>
        /// Use lock if the Input is Used elsewhere;
        /// </summary>
        private int locked = 0;
        public bool Locked { get { return locked > 0; } set { if (value) locked++; else { locked--; if (locked < 0) locked = 0; } } }


        /// <summary>
        /// Associtated interaction (if any).
        /// </summary>
        public EventDto associatedInteraction { get; protected set; }

        /// <summary>
        /// True if the rising edge event has been sent through network (to avoid sending falling edge only).
        /// </summary>
        protected bool risingEdgeEventSent = false;

        EventDisplayer eventDisplayer;

        string toolId;

        protected virtual void Start()
        {
            eventDisplayer = EventMenu.CreateDisplayer();
            eventDisplayer?.Display(false);
        }


        public override void Associate(AbstractInteractionDto interaction, string toolId)
        {
            if (associatedInteraction != null)
            {
                throw new System.Exception("This input is already binded to a interaction ! (" + associatedInteraction + ")");
            }

            if (IsCompatibleWith(interaction))
            {
                associatedInteraction = interaction as EventDto;
                this.toolId = toolId;
                if (associatedInteraction.icon2D != null)
                {
                    Debug.Log("need to work on icon");
                    //HDResourceCache.Download(associatedInteraction.Icon2D, Texture2D =>
                    //{
                    //    if (EventDisplayer != null && associatedInteraction != null && Texture2D != null)
                    //    {
                    //        EventDisplayer.gameObject.SetActive(true);
                    //        EventDisplayer.Set(associatedInteraction.Name, InputLayoutManager.GetInputCode(activationButton).ToString(), Sprite.Create(Texture2D, new Rect(0.0f, 0.0f, Texture2D.width, Texture2D.height), new Vector2(0.5f, 0.5f), 100.0f));
                    //    }
                    //    //else
                    //    //{
                    //    //    EventDisplayer.gameObject.SetActive(true);
                    //    //    EventDisplayer.Set(associatedInteraction.Name, InputLayoutManager.GetInputCode(activationButton).ToString(), null);

                    //    //    Destroy(Texture2D);
                    //    //}
                    //},
                    //webrequest =>
                    //{
                    //    if (EventDisplayer != null && associatedInteraction != null)
                    //    {
                    //        EventDisplayer.gameObject.SetActive(true);
                    //        EventDisplayer.Set(associatedInteraction.Name, InputLayoutManager.GetInputCode(activationButton).ToString(), null);
                    //    }
                    //});
                }
                else
                {
                    if (eventDisplayer != null)
                    {
                        eventDisplayer.Display(true);
                        eventDisplayer.SetUp(associatedInteraction.name, InputLayoutManager.GetInputCode(activationButton).ToString(), null);
                    }
                }

                //if ((!CircularMenu.Exist || !CircularMenu.Instance.IsExpanded) && Input.GetKey(InputLayoutManager.GetInputCode(activationButton)) && !Input.GetKeyDown(InputLayoutManager.GetInputCode(activationButton)) && (associatedInteraction).Hold)
                //{
                //    onInputDown.Invoke();
                //    UMI3DHttpClient.Interact(associatedInteraction.id, new object[2] { true, boneDto.id });
                //    risingEdgeEventSent = true;
                //}
            }
            else
            {
                throw new System.Exception("Trying to associate an uncompatible interaction !");
            }
        }

        protected virtual void Update()
        {
            if (boneDto == null)
                boneDto = AvatarTempo.getBoneID();

            if (LastFrameButton != InputLayoutManager.GetInputCode(activationButton))
            {
                ResetButton();
                LastFrameButton = InputLayoutManager.GetInputCode(activationButton);
            }

            if (associatedInteraction != null && (!CircularMenu.Exists || !CircularMenu.Instance.IsExpanded))
            {
                if (Input.GetKeyDown(InputLayoutManager.GetInputCode(activationButton)))
                {
                    onInputDown.Invoke();
                    if ((associatedInteraction).hold)
                    {
                        var eventdto = new EventStateChangedDto
                        {
                            active = true,
                            boneType = boneDto.boneType,
                            id = associatedInteraction.id,
                            toolId = this.toolId
                        };
                        UMI3DCollaborationClientServer.Send(eventdto, true);
                        risingEdgeEventSent = true;
                    }
                    else
                    {
                        var eventdto = new EventTriggeredDto
                        {
                            boneType = boneDto.boneType,
                            id = associatedInteraction.id,
                            toolId = this.toolId
                        };
                        UMI3DCollaborationClientServer.Send(eventdto, true);
                    }
                }

                if (Input.GetKeyUp(InputLayoutManager.GetInputCode(activationButton)))
                {
                    onInputUp.Invoke();
                    if ((associatedInteraction).hold)
                    {
                        if (risingEdgeEventSent)
                        {
                            var eventdto = new EventStateChangedDto
                            {
                                active = false,
                                boneType = boneDto.boneType,
                                id = associatedInteraction.id,
                                toolId = this.toolId
                            };
                            UMI3DCollaborationClientServer.Send(eventdto, true);
                            risingEdgeEventSent = false;
                        }
                    }
                }
            }
        }

        public override void Associate(ManipulationDto manipulation, DofGroupEnum dofs, string toolId)
        {
            throw new System.NotImplementedException();
        }

        public override AbstractInteractionDto CurrentInteraction()
        {
            return associatedInteraction;
        }

        public override void Dissociate()
        {
            ResetButton();
            eventDisplayer?.Remove();
            associatedInteraction = null;
        }

        void ResetButton()
        {
            if (associatedInteraction != null && (associatedInteraction).hold && risingEdgeEventSent)
            {
                var eventdto = new EventStateChangedDto
                {
                    active = false,
                    boneType = boneDto.boneType,
                    id = associatedInteraction.id,
                    toolId = this.toolId
                };
                UMI3DCollaborationClientServer.Send(eventdto, true);
            }
            risingEdgeEventSent = false;
        }

        public override bool IsCompatibleWith(AbstractInteractionDto interaction)
        {
            return (interaction is EventDto);
        }

        public override bool IsAvailable()
        {
            return associatedInteraction == null && !Locked;
        }
    }
}