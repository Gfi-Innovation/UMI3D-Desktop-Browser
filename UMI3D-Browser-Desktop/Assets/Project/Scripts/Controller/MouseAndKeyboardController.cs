/*
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
using BrowserDesktop.Cursor;
using BrowserDesktop.Interaction;
using BrowserDesktop.Menu;
using BrowserDesktop.Parameters;
using System.Collections.Generic;
using umi3d.cdk;
using umi3d.cdk.interaction;
using umi3d.common;
using umi3d.common.interaction;
using umi3d.common.userCapture;
using UnityEngine;

namespace BrowserDesktop.Controller
{
    public class MouseAndKeyboardController : AbstractController
    {
        static public bool CanProcess = false;

        //public UMI3DBrowserAvatar Avatar;
        public Camera Camera;

        public InteractionMapper InteractionMapper;

        public umi3d.cdk.menu.Menu contextualMenu;

        int navigationDirect = 0;
        [SerializeField]
        List<DofGroupEnum> dofGroups = new List<DofGroupEnum>();
        [SerializeField]
        List<CursorKeyInput> ManipulationActionInput = new List<CursorKeyInput>();

        AutoProjectOnHover reason = new AutoProjectOnHover();

        /// <summary>
        /// Avatar bone linked to this input.
        /// </summary>
        [ConstStringEnum(typeof(BoneType))]
        public string interactionBoneType = BoneType.RightHand;

        [ConstStringEnum(typeof(BoneType))]
        public string hoverBoneType = BoneType.Head;

        Dictionary<int, int> manipulationMap;

        #region Hover

        public struct MouseData
        {
            public bool ForcePorjection;
            public bool ForcePorjectionReleasable;
            public HoldableButtonMenuItem ForceProjectionMenuItem;

            public Interactable OldHovered;
            public string LastHoveredId;
            public Interactable CurentHovered;
            public Transform CurentHoveredTransform;
            public string CurrentHoveredId;

            public Vector3 point;
            public Vector3 worldPoint;
            public Vector3 centeredWorldPoint;
            public Vector3 normal;
            
            public Vector3 worldNormal;
            public Vector3 direction;
            public Vector3 worlDirection;
            public Vector3 cursorOffset;

            public Vector3 lastPoint, lastNormal, lastDirection;



            public HoverState HoverState;

            public int saveDelay;

            public void save()
            {
                if (saveDelay > 0)
                {
                    saveDelay--;
                }
                else
                {
                    if (saveDelay < 0) saveDelay = 0;
                    OldHovered = CurentHovered;
                    LastHoveredId = CurrentHoveredId;
                    CurentHovered = null;
                    CurentHoveredTransform = null;
                    CurrentHoveredId = null;
                    lastPoint = point;
                    lastNormal = normal;
                    lastDirection = direction;
                }
            }

            public bool isDelaying()
            {
                return saveDelay > 0;
            }
        }
        public enum HoverState { None, Hovering, AutoProjected }

        [SerializeField]
        public MouseData mouseData;
        public const float timeToHold = 0.1f;

        #endregion

        #region Inputs

        List<ManipulationGroup> ManipulationInputs = new List<ManipulationGroup>();
        List<KeyInput> KeyInputs = new List<KeyInput>();

        List<KeyMenuInput> KeyMenuInputs = new List<KeyMenuInput>();
        List<FormInput> FormInputs = new List<FormInput>();
        List<LinkInput> LinkInputs = new List<LinkInput>();

        /// <summary>
        /// Instantiated float parameter inputs.
        /// </summary>
        /// <see cref="FindInput(AbstractParameterDto, bool)"/>
        protected List<FloatParameterInput> floatParameterInputs = new List<FloatParameterInput>();

        /// <summary>
        /// Instantiated float range parameter inputs.
        /// </summary>
        /// <see cref="FindInput(AbstractParameterDto, bool)"/>
        protected List<FloatRangeParameterInput> floatRangeParameterInputs = new List<FloatRangeParameterInput>();

        /// <summary>
        /// Instantiated int parameter inputs.
        /// </summary>
        /// <see cref="FindInput(AbstractParameterDto, bool)"/>
        protected List<IntParameterInput> intParameterInputs = new List<IntParameterInput>();

        /// <summary>
        /// Instantiated bool parameter inputs.
        /// </summary>
        /// <see cref="FindInput(AbstractParameterDto, bool)"/>
        protected List<BooleanParameterInput> boolParameterInputs = new List<BooleanParameterInput>();

        /// <summary>
        /// Instantiated string parameter inputs.
        /// </summary>
        /// <see cref="FindInput(AbstractParameterDto, bool)"/>
        protected List<StringParameterInput> stringParameterInputs = new List<StringParameterInput>();

        /// <summary>
        /// Instantiated string enum parameter inputs.
        /// </summary>
        /// <see cref="FindInput(AbstractParameterDto, bool)"/>
        protected List<StringEnumParameterInput> stringEnumParameterInputs = new List<StringEnumParameterInput>();



        public override List<AbstractUMI3DInput> inputs
        {
            get {
                List<AbstractUMI3DInput> list = new List<AbstractUMI3DInput>();
                list.AddRange(ManipulationInputs);
                list.AddRange(KeyInputs);
                list.AddRange(KeyMenuInputs);
                list.AddRange(floatParameterInputs);
                list.AddRange(floatRangeParameterInputs);
                list.AddRange(intParameterInputs);
                list.AddRange(boolParameterInputs);
                list.AddRange(stringParameterInputs);
                list.AddRange(stringEnumParameterInputs);
                return list;
            }
        }

        #endregion

        void CreateForceProjectionMenuItem()
        {
            if (CircularMenu.Exists && mouseData.ForceProjectionMenuItem != null)
            {
                if (!CircularMenu.Instance.menuDisplayManager.menu.Contains(mouseData.ForceProjectionMenuItem))
                {
                    if (mouseData.ForcePorjectionReleasable)
                        CircularMenu.Instance.menuDisplayManager.menu.Add(mouseData.ForceProjectionMenuItem);
                }
                else if (CircularMenu.Instance.menuDisplayManager.menu.Count == 1)
                {
                    DeleteForceProjectionMenuItem();
                }
            }
        }

        void UnequipeForceProjection()
        {
            InteractionMapper.ReleaseTool(currentTool.id, new RequestedByUser());
            mouseData.ForcePorjection = false;
            mouseData.CurentHovered = null;
            mouseData.CurentHoveredTransform = null;
            mouseData.OldHovered = null;
            CircularMenu.Collapse();
            mouseData.HoverState = HoverState.None;
        }

        void ForceProjectionMenuItem(bool pressed)
        {
            if (mouseData.ForcePorjectionReleasable)
            {
                DeleteForceProjectionMenuItem();
                UnequipeForceProjection();
            }
        }

        void DeleteForceProjectionMenuItem()
        {
            if (mouseData.ForceProjectionMenuItem != null)
            {
                if (CircularMenu.Exists)
                {
                    CircularMenu.Instance.menuDisplayManager.menu.Remove(mouseData.ForceProjectionMenuItem);
                }
            }
        }

        public void Awake()
        {
            foreach (KeyInput input in GetComponentsInChildren<KeyInput>())
            {
                KeyInputs.Add(input);
                input.Init(this);
                input.bone = interactionBoneType;
            }

            mouseData.ForceProjectionMenuItem = new HoldableButtonMenuItem
            {
                Name = "Release",
                Holdable = false
            };
            mouseData.ForceProjectionMenuItem.Subscribe(ForceProjectionMenuItem);

            mouseData.saveDelay = 0;
        }

        void InputDown(KeyInput input) { }
        void InputUp(KeyInput input) { }

        private void Update()
        {
            if (Input.GetKeyDown(InputLayoutManager.GetInputCode(InputLayoutManager.Input.ContextualMenuNavigationDirect)) || Input.mouseScrollDelta.y < 0)
            {
                navigationDirect++;
            }
            else if (Input.mouseScrollDelta.y > 0)
            {
                navigationDirect--;
            }
        }


        private void LateUpdate()
        {
            CursorHandler.Instance.ExitIndicator = mouseData.ForcePorjection;
            if (CanProcess)
            {
                if (MainMenu.IsDisplaying)
                {
                    mouseData.save();
                    Hover();
                }
                else
                {
                    if (navigationDirect != 0)
                    {
                        if (navigationDirect > 0)
                            ManipulationInput.NextManipulation();
                        else
                            ManipulationInput.PreviousManipulation();
                        navigationDirect = 0;
                    }
                    MouseHandler();
                }
            }
        }

        public void CircularMenuColapsed()
        {
            if (mouseData.CurentHovered == null) return;
            // CircularMenu.Instance.MenuColapsed.RemoveListener(CircularMenuColapsed);
            CursorHandler.State = CursorHandler.CursorState.Hover;
            mouseData.saveDelay = 3;
        }

        void MouseHandler()
        {
            if (!(
                        mouseData.HoverState == HoverState.AutoProjected
                        && (CursorHandler.State == CursorHandler.CursorState.Clicked || SideMenu.IsExpanded)
               ))
            {
                mouseData.save();
                Vector3 screenPosition = Input.mousePosition;
                Ray ray = Camera.main.ScreenPointToRay(screenPosition);
                Debug.DrawRay(ray.origin, ray.direction.normalized * 100f, Color.red, 0, true);
                RaycastHit[] hits = umi3d.common.Physics.RaycastAll(ray, 100f);
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider.gameObject.GetComponentInParent<UMI3DEnvironmentLoader>() == null)
                        continue;
                    var Interactable = hit.collider.gameObject.GetComponent<InteractableContainer>();
                    if (Interactable == null)
                        Interactable = hit.collider.gameObject.GetComponentInParent<InteractableContainer>();
                    if (Interactable != null && Interactable.Interactable.Active)
                    {
                        mouseData.CurrentHoveredId = UMI3DEnvironmentLoader.GetNodeID(hit.collider);

                        mouseData.CurentHovered = Interactable.Interactable;
                        mouseData.CurentHoveredTransform = Interactable.transform;

                        mouseData.point = Interactable.transform.InverseTransformPoint(hit.point);
                        mouseData.worldPoint = hit.point;
                        if (Vector3.Distance(mouseData.worldPoint, hit.transform.position) < 0.1f) mouseData.centeredWorldPoint = hit.transform.position;
                        else mouseData.centeredWorldPoint = mouseData.worldPoint;

                        mouseData.normal = Interactable.transform.InverseTransformDirection(hit.normal);
                        mouseData.worldNormal = hit.normal;

                        mouseData.direction = Interactable.transform.InverseTransformDirection(ray.direction);
                        mouseData.worlDirection = ray.direction;

                        break;
                    }
                }
                Hover();
            }
            else
            {
                //CircularMenu.Instance.Follow(mouseData.centeredWorldPoint);
            }
        }

        void Hover()
        {
            if (mouseData.ForcePorjection)
            {
                if (CircularMenu.Exists && !CircularMenu.Instance.IsEmpty())
                {
                    CreateForceProjectionMenuItem();
                }
                else if (Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.LeaveContextualMenu))
                    ||
                    Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.ContextualMenuNavigationBack)))
                {
                    if(mouseData.ForcePorjectionReleasable)
                        UnequipeForceProjection();
                }
            }
            else
            {
                if (mouseData.CurentHovered != null)
                {
                    if (mouseData.CurentHovered != mouseData.OldHovered)
                    {
                        if (mouseData.OldHovered != null)
                        {
                            if (mouseData.HoverState == HoverState.AutoProjected)
                            {
                                InteractionMapper.ReleaseTool(currentTool.id, new RequestedByUser());
                            }
                            mouseData.OldHovered.HoverExit(hoverBoneType, mouseData.LastHoveredId, mouseData.lastPoint, mouseData.lastNormal, mouseData.lastDirection);
                            CircularMenu.Collapse();
                            mouseData.OldHovered = null;
                        }
                        mouseData.HoverState = HoverState.Hovering;
                        if (mouseData.CurentHovered.dto.interactions.Count > 0 && IsCompatibleWith(mouseData.CurentHovered))
                        {
                            InteractionMapper.SelectTool(mouseData.CurentHovered.dto.id,true, this, mouseData.CurrentHoveredId, reason);
                            CursorHandler.State = CursorHandler.CursorState.Hover;
                            mouseData.HoverState = HoverState.AutoProjected;
                            CircularMenu.Instance.MenuColapsed.AddListener(CircularMenuColapsed);
                            mouseData.OldHovered = mouseData.CurentHovered;
                        }
                        mouseData.CurentHovered.HoverEnter(hoverBoneType, mouseData.CurrentHoveredId, mouseData.point, mouseData.normal, mouseData.direction);
                    }
                    else
                    {
                        if (mouseData.LastHoveredId != null && mouseData.CurrentHoveredId != mouseData.LastHoveredId)
                        {
                            if (associatedInputs.ContainsKey(mouseData.CurentHovered.dto.id))
                            {
                                foreach (var input in associatedInputs[mouseData.CurentHovered.dto.id])
                                    input.UpdateHoveredObjectId(mouseData.CurrentHoveredId);
                            }
                        }
                    }

                    mouseData.CurentHovered.Hovered(hoverBoneType, mouseData.CurrentHoveredId, mouseData.point, mouseData.normal, mouseData.direction);
                }
                else if (mouseData.OldHovered != null)
                {
                    if (mouseData.HoverState == HoverState.AutoProjected)
                    {
                        CircularMenu.Instance.MenuColapsed.RemoveListener(CircularMenuColapsed);
                        InteractionMapper.ReleaseTool(currentTool.id, new RequestedByUser());
                    }
                    mouseData.OldHovered.HoverExit(hoverBoneType, mouseData.LastHoveredId, mouseData.lastPoint, mouseData.lastNormal, mouseData.lastDirection);
                    CircularMenu.Collapse();
                    CursorHandler.State = CursorHandler.CursorState.Default;
                    mouseData.OldHovered = null;
                    mouseData.HoverState = HoverState.None;
                }
            }
        }

        bool ShouldAutoProject(InteractableDto tool)
        {
            List<AbstractInteractionDto> manips = tool.interactions.FindAll(x => x is ManipulationDto);
            List<AbstractInteractionDto> events = tool.interactions.FindAll(x => x is EventDto);
            List<AbstractInteractionDto> parameters = tool.interactions.FindAll(x => x is AbstractParameterDto);
            return (((parameters.Count == 0) && (events.Count <= 7) && (manips.Count == 0)));
        }

        public override void Clear()
        {
            foreach (ManipulationGroup input in ManipulationInputs)
            {
                if (!input.IsAvailable())
                    input.Dissociate();
            }
            foreach (KeyInput input in KeyInputs)
            {
                if (!input.IsAvailable())
                    input.Dissociate();
            }
            ClearParameters();
        }

        public void ClearParameters()
        {
            KeyMenuInputs.ForEach((a) => { Destroy(a); });
            KeyMenuInputs = new List<KeyMenuInput>();

            floatParameterInputs.ForEach((a) => { Destroy(a); });
            floatParameterInputs = new List<FloatParameterInput>();
            floatRangeParameterInputs.ForEach((a) => { Destroy(a); });
            floatRangeParameterInputs = new List<FloatRangeParameterInput>();
            intParameterInputs.ForEach((a) => { Destroy(a); });
            intParameterInputs = new List<IntParameterInput>();
            boolParameterInputs.ForEach((a) => { Destroy(a); });
            boolParameterInputs = new List<BooleanParameterInput>();
            stringParameterInputs.ForEach((a) => { Destroy(a); });
            stringParameterInputs = new List<StringParameterInput>();
            stringEnumParameterInputs.ForEach((a) => { Destroy(a); });
            stringEnumParameterInputs = new List<StringEnumParameterInput>();
        }

        /// <summary>
        /// Create a menu to access each interactions of a tool separately.
        /// </summary>
        /// <param name="interactions"></param>
        public override void CreateInteractionsMenuFor(AbstractTool tool)
        {
            Debug.Log("oups");
        }

        /// <summary>
        /// Check if a tool can be projected on this controller.
        /// </summary>
        /// <param name="tool"> The tool to be projected.</param>
        /// <returns></returns>
        public override bool IsCompatibleWith(AbstractTool tool)
        {
            List<AbstractInteractionDto> interactions = tool.interactions;
            List<AbstractInteractionDto> manips = interactions.FindAll(x => x is ManipulationDto);
            foreach (var man in manips)
            {
                var man2 = man as ManipulationDto;
                if (
                    !man2.dofSeparationOptions.Exists((sep) =>
                         {
                             foreach (DofGroupDto dof in sep.separations)
                             {
                                 if (!dofGroups.Contains(dof.dofs))
                                     return false;
                             }
                             return true;
                         }))
                    return false;
            }
            return true;

        }

        /// <summary>
        /// Check if a tool requires the generation of a menu to be projected.
        /// </summary>
        /// <param name="tool"> The tool to be projected.</param>
        /// <returns></returns>
        public override bool RequiresMenu(AbstractTool tool)
        {
            List<AbstractInteractionDto> interactions = tool.interactions;
            List<AbstractInteractionDto> manips = interactions.FindAll(x => x is ManipulationDto);
            List<AbstractInteractionDto> events = interactions.FindAll(x => x is EventDto);
            //List<AbstractInteractionDto> parameters = tool.Interactions.FindAll(x => x is AbstractParameterDto);
            // return ((events.Count > 7 || manips.Count > 0) && (events.Count > 6 || manips.Count > 1));
            return false; // (/*(parameters.Count > 0) ||*/ (events.Count > 7) || (manips.Count > 1) || ((manips.Count > 0) && (events.Count > 6)));
        }

        public bool RequiresParametersMenu(AbstractTool tool)
        {
            List<AbstractInteractionDto> interactions = tool.interactions;
            List<AbstractInteractionDto> parameters = interactions.FindAll(x => x is AbstractParameterDto);
            // return ((events.Count > 7 || manips.Count > 0) && (events.Count > 6 || manips.Count > 1));
            return (parameters.Count > 0);
        }

        protected override bool isInteracting()
        {
            throw new System.NotImplementedException();
        }

        protected override bool isNavigating()
        {
            throw new System.NotImplementedException();
        }

        public override DofGroupOptionDto FindBest(DofGroupOptionDto[] options)
        {

            foreach (var GroupOption in options)
            {
                bool ok = true;
                foreach (DofGroupDto dof in GroupOption.separations)
                {
                    if (!dofGroups.Contains(dof.dofs))
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok) return GroupOption;
            }

            throw new System.NotImplementedException();
        }



        public override AbstractUMI3DInput FindInput(ManipulationDto manip, DofGroupDto dof, bool unused = true)
        {
            ManipulationGroup group = ManipulationInputs.Find(i => i.IsAvailableFor(manip));
            if (group == null)
            {
                group = ManipulationGroup.Instanciate(this, ManipulationActionInput, dofGroups, transform);
                if (group == null)
                {
                    Debug.LogWarning("find manip input FAILED");
                    return null;
                }
                group.bone = interactionBoneType;
                ManipulationInputs.Add(group);
            }
            return group;
        }

        public override AbstractUMI3DInput FindInput(EventDto evt, bool unused = true)
        {
            KeyInput input = KeyInputs.Find(i => i.IsAvailable() || !unused);
            if (input == null)
            {
                KeyMenuInput inputMenu = KeyMenuInputs.Find(i => i.IsAvailable() || !unused);
                if (inputMenu == null)
                {
                    inputMenu = this.gameObject.AddComponent<KeyMenuInput>();
                    inputMenu.bone = interactionBoneType;
                    KeyMenuInputs.Add(inputMenu);
                }
                return inputMenu;
            }
            return input;
        }

        public override AbstractUMI3DInput FindInput(FormDto form, bool unused = true)
        {
            FormInput inputMenu = FormInputs.Find(i => i.IsAvailable() || !unused);
            if (inputMenu == null)
            {
                inputMenu = this.gameObject.AddComponent<FormInput>();
                inputMenu.bone = interactionBoneType;
                FormInputs.Add(inputMenu);
            }
            return inputMenu;
        }

        public override AbstractUMI3DInput FindInput(LinkDto link, bool unused = true)
        {
            LinkInput inputMenu = LinkInputs.Find(i => i.IsAvailable() || !unused);
            if (inputMenu == null)
            {
                inputMenu = this.gameObject.AddComponent<LinkInput>();
                inputMenu.bone = interactionBoneType;
                LinkInputs.Add(inputMenu);
            }
            return inputMenu;
        }

        public override AbstractUMI3DInput FindInput(AbstractParameterDto param, bool unused = true)
        {
            if (param is FloatRangeParameterDto)
            {
                FloatRangeParameterInput floatRangeInput = floatRangeParameterInputs.Find(i => i.IsAvailable());
                if (floatRangeInput == null)
                {
                    floatRangeInput = this.gameObject.AddComponent<FloatRangeParameterInput>();
                    floatRangeParameterInputs.Add(floatRangeInput);
                }
                return floatRangeInput;
            }
            else if (param is FloatParameterDto)
            {
                FloatParameterInput floatInput = floatParameterInputs.Find(i => i.IsAvailable());
                if (floatInput == null)
                {
                    floatInput = this.gameObject.AddComponent<FloatParameterInput>();
                    floatParameterInputs.Add(floatInput);
                }
                return floatInput;
            }
            else if (param is IntegerParameterDto)
            {
                IntParameterInput intInput = intParameterInputs.Find(i => i.IsAvailable());
                if (intInput == null)
                {
                    intInput = new IntParameterInput();
                    intParameterInputs.Add(intInput);
                }
                return intInput;

            }
            else if (param is IntegerRangeParameterDto)
            {
                throw new System.NotImplementedException();
            }
            else if (param is BooleanParameterDto)
            {
                BooleanParameterInput boolInput = boolParameterInputs.Find(i => i.IsAvailable());
                if (boolInput == null)
                {
                    boolInput = this.gameObject.AddComponent<BooleanParameterInput>();
                    boolParameterInputs.Add(boolInput);
                }
                return boolInput;
            }
            else if (param is StringParameterDto)
            {
                StringParameterInput stringInput = stringParameterInputs.Find(i => i.IsAvailable());
                if (stringInput == null)
                {
                    stringInput = this.gameObject.AddComponent<StringParameterInput>();
                    stringParameterInputs.Add(stringInput);
                }
                return stringInput;
            }
            else if (param is EnumParameterDto<string>)
            {
                StringEnumParameterInput stringEnumInput = stringEnumParameterInputs.Find(i => i.IsAvailable());
                if (stringEnumInput == null)
                {
                    stringEnumInput = this.gameObject.AddComponent<StringEnumParameterInput>();
                    stringEnumParameterInputs.Add(stringEnumInput);
                }
                return stringEnumInput;
            }
            else
            {
                return null;
            }
        }

        public override void Release(AbstractTool tool, InteractionMappingReason reason)
        {
            //try
            //{
            base.Release(tool, reason);
            if (reason is ToolNeedToBeUpdated && tool.interactions.Count > 0) return;

            if (mouseData.CurentHovered != null && mouseData.CurentHovered.dto.id == tool.id)
            {
                mouseData.CurentHovered = null;
                mouseData.CurentHoveredTransform = null;
                mouseData.HoverState = HoverState.None;
                CursorHandler.State = CursorHandler.CursorState.Default;
            }
            if (mouseData.ForcePorjection)
            {
                mouseData.ForcePorjection = false;
                DeleteForceProjectionMenuItem();
            }
            tool.onReleased(interactionBoneType);
            //}
            //catch { }
        }

        public override void Project(AbstractTool tool, bool releasable, InteractionMappingReason reason, string hoveredObjectId)
        {
            base.Project(tool, releasable, reason, hoveredObjectId); ;
            if (reason is RequestedByEnvironment)
            {
                mouseData.ForcePorjection = true;
                mouseData.ForcePorjectionReleasable = releasable;
            }
            tool.onProjected(interactionBoneType);
        }

        protected override string GetCurrentHoveredId()
        {
            return mouseData.CurrentHoveredId;
        }

    }
}