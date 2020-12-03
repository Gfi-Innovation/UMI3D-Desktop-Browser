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
using System;
using System.Collections.Generic;
using System.Linq;
using umi3d.cdk;
using umi3d.cdk.interaction;
using umi3d.cdk.menu;
using umi3d.common.interaction;

public class InteractionMapper : AbstractInteractionMapper
{
    public new static InteractionMapper Instance { get { return AbstractInteractionMapper.Instance as InteractionMapper; } }

    /// <summary>
    /// If an interactable is loaded before its gameobject, the interaction mapper will wait <see cref="interactableLoadTimeout"/> seconds before destroying it.
    /// </summary>
    public float interactableLoadTimeout = 60;

    /// <summary>
    /// Menu to store toolboxes into.
    /// </summary>
    public Menu toolboxMenu;

    #region Data

    /// <summary>
    /// Associate a toolid with the controller the tool is projected on.
    /// </summary>
    public Dictionary<string, AbstractController> toolIdToController { get; protected set; } = new Dictionary<string, AbstractController>();

    /// <summary>
    /// Id to Dto interactions map.
    /// </summary>
    public Dictionary<string, AbstractInteractionDto> interactionsIdToDto { get; protected set; } = new Dictionary<string, AbstractInteractionDto>();

    /// <summary>
    /// Currently projected tools.
    /// </summary>
    private Dictionary<string, InteractionMappingReason> projectedTools = new Dictionary<string, InteractionMappingReason>();

    #endregion

    /// <summary>
    /// Default local toolbox, for environments' tools with no toolbox set.
    /// </summary>
    //protected Toolbox defaultToolbox;
    //ToolboxSubMenu defaultToolboxMenu;

    //protected override void Awake()
    //{
    //    base.Awake();

    //    defaultToolbox = new Toolbox(new ToolboxDto
    //    {
    //        id = "defaultLocalToolbox",
    //        name = "default toolbox",
    //        description = "Local default toolbox"
    //    });
    //    defaultToolboxMenu = new ToolboxSubMenu()
    //    {
    //        Name = "default",
    //        navigable = true,
    //        toolbox = defaultToolbox,
    //        SubMenu = new List<Menu>()
    //    };
    //    StartCoroutine(SetDefaultToolbox(defaultToolboxMenu));
    //}

    /// <summary>
    /// Reset the InteractionMapper module
    /// </summary>
    public override void ResetModule()
    {
        foreach (AbstractController c in Controllers)
            c.Clear();

        if (toolboxMenu != null)
        {
            toolboxMenu.RemoveAllSubMenu();
            toolboxMenu.RemoveAllMenuItem();
        }

        toolIdToController = new Dictionary<string, AbstractController>();
        //if (defaultToolboxMenu != null)
        //{
        //    defaultToolboxMenu.SubMenu = new List<Menu>();
        //    defaultToolboxMenu.MenuItems = new List<MenuItem>();
        //}
    }

    //IEnumerator SetDefaultToolbox(ToolboxSubMenu defaultToolboxMenu)
    //{
    //    var wait = new WaitForEndOfFrame();
    //    while (toolboxMenu == null || toolboxMenu == null) yield return wait;
    //    toolboxMenu.Add(defaultToolboxMenu);
    //}


    /// <summary>
    /// Select the best compatible controller for a given tool (not necessarily available).
    /// </summary>
    /// <param name="tool"></param>
    /// <returns></returns>
    protected AbstractController GetController(AbstractTool tool)
    {
        foreach (AbstractController controller in Controllers)
        {
            if (controller.IsCompatibleWith(tool) && controller.IsAvailableFor(tool))
            {
                return controller;
            }
        }

        foreach (AbstractController controller in Controllers)
        {
            if (controller.IsCompatibleWith(tool))
            {
                return controller;
            }
        }

        return null;
    }

    /// <summary>
    /// Request a Tool to be released.
    /// </summary>
    /// <param name="dto">The tool to be released</param>
    public override void ReleaseTool(string toolId, InteractionMappingReason reason = null)
    {
        AbstractTool tool = GetTool(toolId);

        if (toolIdToController.TryGetValue(tool.id, out AbstractController controller))
        {
            controller.Release(tool,reason);
            toolIdToController.Remove(tool.id);
            tool.OnUpdated.RemoveAllListeners();
            projectedTools.Remove(tool.id);
        }
        else
        {
            throw new Exception("Tool not selected");
        }
    }

    /// <summary>
    /// Request the selection of a Tool.
    /// Be careful, this method could be called before the tool is added for async loading reasons
    /// </summary>
    /// <param name="dto">The tool to select</param>
    public override bool SelectTool(string toolId, bool releasable, string hoveredObjectId, InteractionMappingReason reason = null)
    {
        AbstractTool tool = GetTool(toolId);
        if (tool == null)
            throw new Exception("tool does not exist");

        if (toolIdToController.ContainsKey(tool.id))
        {
            throw new System.Exception("Tool already projected");
        }

        AbstractController controller = GetController(tool);
        if (controller != null)
        {
            if (!controller.IsAvailableFor(tool))
            {
                if (ShouldForceProjection(controller, tool, reason))
                {
                    ReleaseTool(controller.tool.id);
                }
                else
                {
                    return false;
                }
            }

            return SelectTool(tool.id,releasable, controller, hoveredObjectId, reason);
        }
        else
        {
            throw new System.Exception("No controller is compatible with this tool");
        }
    }

    /// <summary>
    /// Request the selection of a Tool for a given controller.
    /// Be careful, this method could be called before the tool is added for async loading reasons.
    /// </summary>
    /// <param name="tool">The tool to select</param>
    /// <param name="controller">Controller to project the tool on</param>
    public bool SelectTool(string toolId, bool releasable, AbstractController controller, string hoveredObjectId, InteractionMappingReason reason = null)
    {
        AbstractTool tool = GetTool(toolId);
        if (controller.IsCompatibleWith(tool))
        {
            if (toolIdToController.ContainsKey(tool.id))
            {
                ReleaseTool(tool.id, new SwitchController());
            }

            toolIdToController.Add(tool.id, controller);
            projectedTools.Add(tool.id, reason);
            tool.OnUpdated.AddListener(() => UpdateTools(toolId,releasable,reason));
            controller.Project(tool,releasable,reason,hoveredObjectId);

            return true;
        }
        else
        {
            throw new System.Exception("This controller is not compatible with this tool");
        }
    }

    /// <summary>
    /// Request a Tool to be replaced by another one.
    /// </summary>
    /// <param name="selected">The tool to be selected</param>
    /// <param name="released">The tool to be released</param>
    public override bool UpdateTools(string toolId, bool releasable, InteractionMappingReason reason = null)
    {
        if (toolIdToController.ContainsKey(toolId))
        {
            AbstractController controller = toolIdToController[toolId];
            AbstractTool tool = GetTool(toolId);
            if (tool.interactions.Count <= 0)
                controller.Release(tool, new ToolNeedToBeUpdated());
            else
                controller.Update(tool, releasable, reason);
            return true;
        }
        throw new System.Exception("no controller have this tool projected");
    }

    /// <summary>
    /// Request a Tool to be replaced by another one.
    /// </summary>
    /// <param name="selected">The tool to be selected</param>
    /// <param name="released">The tool to be released</param>
    public override bool SwitchTools(string select, string release, bool releasable, string hoveredObjectId, InteractionMappingReason reason = null)
    {
        ReleaseTool(release);
        if (!SelectTool(select,releasable, hoveredObjectId, reason))
        {
            if (SelectTool(release,releasable, hoveredObjectId))
                return false;
            else
                throw new Exception("Internal error");
        }
        return true;
    }

    //this function will change/move in the future.
    protected bool ShouldForceProjection(AbstractController controller, AbstractTool tool, InteractionMappingReason reason)
    {
        if (controller.IsAvailableFor(tool))    
            return true;

        if (controller.tool == null)
            return true; //check here

        if (projectedTools.TryGetValue(controller.tool.id, out InteractionMappingReason lastProjectionReason))
        {
            //todo : add some intelligence here.
            return !(reason is AutoProjectOnHover);
        }
        else
        {
            throw new Exception("Internal error");
        }
    }

    public override bool IsToolSelected(string toolId)
    {
        return projectedTools.ContainsKey(toolId);
    }


    #region CRUD

    //#region Create

    ///// <summary>
    ///// Create new interaction toolboxes.
    ///// </summary>
    ///// <param name="dtos">A list of ToolboxDto representing the toolboxes to be created</param>
    //public override IEnumerable<Toolbox> CreateToolboxes(IEnumerable<ToolboxDto> dtos)
    //{
    //    List<Toolbox> toolboxes = new List<Toolbox>();
    //    foreach (ToolboxDto dto in dtos)
    //    {
    //        toolboxes.Add(CreateToolbox(dto));
    //    }
    //    return toolboxes;
    //}

    ///// <summary>
    ///// Create a new interaction toolbox.
    ///// </summary>
    ///// <param name="dtos">ToolboxDto representing the toolbox to create</param>
    //public override Toolbox CreateToolbox(ToolboxDto dto)
    //{
    //    Toolbox toolbox = new Toolbox(dto);

    //    ToolboxSubMenu sub = new ToolboxSubMenu()
    //    {
    //        Name = toolbox.dto.name,
    //        toolbox = toolbox
    //    };

    //    toolboxesIdToMenu.Add(dto.id, sub);
    //    toolboxMenu.Add(sub);

    //    CreateTools(dto.tools);

    //    return toolbox;
    //}

    public override void CreateToolbox(Toolbox toolbox)
    {
        toolboxMenu.Add(toolbox.sub);
    }
    public override void CreateTool(Tool tool)
    {
        foreach (var interaction in tool.dto.interactions)
        {
            interactionsIdToDto[interaction.id] = interaction;
        }
        tool.Menu.Subscribe(() =>
        {
            if (tool.Menu.toolSelected)
            {
                ReleaseTool(tool.id, new RequestedFromMenu());
            }
            else
            {
                SelectTool(tool.id,true,null, new RequestedFromMenu());
            }
        });
    }

    ///// <summary>
    ///// Create new interaction tools.
    ///// </summary>
    ///// <param name="dtos">A list of ToolDto representing the tools to be inserted in existing toolboxes</param>
    //public override IEnumerable<AbstractTool> CreateTools(IEnumerable<AbstractToolDto> dtos)
    //{

    //    List<AbstractTool> tools = new List<AbstractTool>();
    //    foreach (AbstractToolDto dto in dtos)
    //    {
    //        Debug.Log(dto.name);
    //        tools.Add(CreateTool(dto));
    //    }

    //    return tools;
    //}

    ///// <summary>
    ///// Create a new interaction tool.
    ///// </summary>
    ///// <param name="dto">ToolDto representing the tool to be inserted in existing toolbox</param>
    //public override AbstractTool CreateTool(AbstractToolDto dto)
    //{
    //    if (dto is ToolDto)
    //        return CreateToolInternal(dto as ToolDto);
    //    else if (dto is InteractableDto)
    //        return CreateInteractable(dto as InteractableDto);
    //    else
    //        throw new Exception("Type not supported");

    //}

    //protected Tool CreateToolInternal(ToolDto dto)
    //{
    //    if (ToolExists(dto.id))
    //        return null;

    //    ToolDto tooldto = dto as ToolDto;

    //    //if ((tooldto.id == null) || (tooldto.id == ""))
    //    //    tooldto.id = defaultToolbox.dto.id;
    //    //else if (!ToolboxExists(tooldto.id))
    //    //    throw new Exception("Toolbox not found");

    //    //Toolbox toolbox = GetToolbox(tooldto.id);
    //    //toolbox.tools.Add(tooldto.id);

    //    Tool tool = GetTool(dto.id) as Tool;
    //    //Tool tool = this.gameObject.AddComponent<Tool>();
    //    //tool.SetFromDto(tooldto);

    //    if (toolboxesIdToMenu.TryGetValue(tooldto.id, out ToolboxSubMenu toolboxSubMenu))
    //    {
    //        ToolMenuItem toolMenuItem = new ToolMenuItem()
    //        {
    //            tool = tool,
    //            Name = tooldto.name
    //        };

    //        toolMenuItem.Subscribe(() =>
    //        {
    //            if (toolMenuItem.toolSelected)
    //            {
    //                ReleaseTool(tooldto.id, new RequestedFromMenu());
    //            }
    //            else
    //            {
    //                SelectTool(tooldto.id, new RequestedFromMenu());
    //            }
    //        });

    //        toolsIdToMenu.Add(tooldto.id, toolMenuItem);
    //    }
    //    else
    //    {
    //        throw new Exception("No menu found for the tool");
    //    }

    //    foreach (AbstractInteractionDto inter in tooldto.interactions)
    //    {
    //        if (!InteractionExists(inter.id))
    //            interactionsIdToDto.Add(inter.id, inter);
    //    }

    //    return tool;
    //}

    ///// <summary>
    ///// Create new interaction tools.
    ///// </summary>
    ///// <param name="dtos">A list of ToolDto representing the tools to be inserted in existing toolboxes</param>
    //public override IEnumerable<Interactable> CreateInteractables(IEnumerable<InteractableDto> dtos, IEnumerable<GameObject> interactableObjects)
    //{
    //    List<Interactable> interactables = new List<Interactable>();
    //    List<InteractableDto> ldtos = new List<InteractableDto>(dtos);
    //    List<GameObject> linterObj = new List<GameObject>(interactableObjects);

    //    if (ldtos.Count != linterObj.Count)
    //        throw new Exception("There should be as much as dtos as gameobjects");

    //    for (int i = 0; i < ldtos.Count; i++)
    //    {
    //        interactables.Add(CreateInteractable(ldtos[i], linterObj[i]));
    //    }

    //    return interactables;
    //}

    //public override Interactable CreateInteractable(InteractableDto dto, GameObject interactableObject = null)
    //{
    //    if (ToolExists(dto.id))
    //        throw new Exception("This interactable already exists.");

    //    if (interactableObject == null)
    //        interactableObject = (UMI3DEnvironmentLoader.GetEntity(dto.id) as UMI3DNodeInstance)?.gameObject;
    //    if (interactableObject == null)
    //        throw new Exception("Interactable gameobject not found");

    //    Interactable interactable = interactableObject.AddComponent<Interactable>();
    //    interactable.SetFromDto(dto as InteractableDto);

    //    foreach (AbstractInteractionDto inter in dto.interactions)
    //    {
    //        if (!InteractionExists(inter.id))
    //            interactionsIdToDto.Add(inter.id, inter);
    //    }
    //    return interactable;
    //}

    //public override int CreateInteractions(IEnumerable<AbstractInteractionDto> dtos)
    //{
    //    int count = 0;
    //    foreach (AbstractInteractionDto inter in dtos)
    //        if (CreateInteraction(inter))
    //            count++;
    //    return count;
    //}

    //public override bool CreateInteraction(AbstractInteractionDto dto)
    //{
    //    if (InteractionExists(dto.id) || !ToolExists(dto.ToolId))
    //        return false;

    //    AbstractTool tool = GetTool(dto.ToolId);
    //    if (tool.interactions.Contains(dto.ToolId))
    //        return false;

    //    tool.interactions.Add(dto.id);
    //    interactionsIdToDto.Add(dto.id, dto);
    //    return true;
    //}

    //#endregion

    //#region Read

    public override Toolbox GetToolbox(string id)
    {
        if (!ToolboxExists(id))
            throw new KeyNotFoundException();
        return UMI3DEnvironmentLoader.GetEntity(id)?.Object as Toolbox;
    }

    public override IEnumerable<Toolbox> GetToolboxes(Predicate<Toolbox> condition)
    {
        return Toolbox.Toolboxes().FindAll(condition);
    }

    public override AbstractTool GetTool(string id)
    {
        if (!ToolExists(id))
            throw new KeyNotFoundException();
        return UMI3DEnvironmentLoader.GetEntity(id)?.Object as AbstractTool;
    }

    public override IEnumerable<AbstractTool> GetTools(Predicate<AbstractTool> condition)
    {
        return UMI3DEnvironmentLoader.Entities().Where(e => e?.Object is AbstractTool).Select(e => e?.Object as AbstractTool).ToList().FindAll(condition);
    }

    public override AbstractInteractionDto GetInteraction(string id)
    {
        if (!InteractionExists(id))
            throw new KeyNotFoundException();
        interactionsIdToDto.TryGetValue(id, out AbstractInteractionDto inter);
        return inter;
    }

    public override IEnumerable<AbstractInteractionDto> GetInteractions(Predicate<AbstractInteractionDto> condition)
    {
        return interactionsIdToDto.Values.ToList().FindAll(condition);
    }

    public override bool ToolboxExists(string id)
    {
        return (UMI3DEnvironmentLoader.GetEntity(id)?.Object as Toolbox) != null;
    }

    public override bool ToolExists(string id)
    {
        return (UMI3DEnvironmentLoader.GetEntity(id)?.Object as AbstractTool) != null;
    }

    public override bool InteractionExists(string id)
    {
        return interactionsIdToDto.ContainsKey(id);
    }

    public override AbstractController GetController(string projectedToolId)
    {
        if (!IsToolSelected(projectedToolId))
            return null;

        toolIdToController.TryGetValue(projectedToolId, out AbstractController controller);
        return controller;
    }

    //#endregion

    //#region Update
    //public override void UpdateToolboxes(IEnumerable<ToolboxDto> dtos)
    //{
    //    foreach (ToolboxDto dto in dtos)
    //    {
    //        UpdateToolbox(dto);
    //    }
    //}

    //public override void UpdateToolbox(ToolboxDto dto)
    //{
    //    if (!ToolboxExists(dto.id))
    //        return;

    //    Toolbox toolbox = GetToolbox(dto.id);
    //    //toolbox.UpdateFromDto(dto);

    //    if (toolboxesIdToMenu.TryGetValue(dto.id, out ToolboxSubMenu tbsm))
    //    {
    //        tbsm.toolbox = toolbox;
    //        tbsm.Name = dto.name;
    //    }
    //}

    //public override void UpdateTools(IEnumerable<AbstractToolDto> dtos)
    //{
    //    foreach (AbstractToolDto dto in dtos)
    //    {
    //        UpdateTool(dto);
    //    }
    //}

    //public override void UpdateTool(AbstractToolDto dto)
    //{
    //    if (!ToolExists(dto.id))
    //        return;

    //    AbstractTool tool = GetTool(dto.id);
    //    if (dto is ToolDto)
    //    {
    //        tool.SetFromDto(dto as ToolDto);

    //        if (toolsIdToMenu.TryGetValue((dto as ToolDto).toolboxId, out ToolMenuItem toolMenuItem))
    //        {
    //            toolMenuItem.Name = dto.name;
    //            toolMenuItem.tool = tool as Tool;
    //        }
    //        else
    //        {
    //            throw new Exception("No menu found for the tool");
    //        }
    //    }
    //    else if (dto is InteractableDto)
    //    {
    //        string oldObjId = (tool as Interactable).dto.objectId;
    //        tool.SetFromDto(dto as InteractableDto);
    //        string newObjId = (tool as Interactable).dto.objectId;

    //        if (!oldObjId.Equals(newObjId))
    //            ChangeInteractableObject(dto.id, oldObjId, newObjId);
    //    }
    //    else
    //    {
    //        throw new Exception("This type is not supported.");
    //    }

    //    UpdateInteractions(dto.interactions);
    //}

    ///// <summary>
    ///// Transfer an interactable component from a gameobject to an other
    ///// </summary>
    //protected void ChangeInteractableObject(string interactableId, string oldObjectId, string newObjectId)
    //{
    //    GameObject currentObject = UMI3DBrowser.Scene.GetObject(oldObjectId);
    //    GameObject newObject = UMI3DBrowser.Scene.GetObject(newObjectId);

    //    if ((currentObject == null) || (newObject == null))
    //        throw new Exception("Internal error : gameobject not found !");

    //    Interactable inter = currentObject.GetComponents<Interactable>().First(i => i.id.Equals(interactableId));
    //    if (inter == null)
    //        throw new Exception("Internal error : interactable not found !");

    //    Interactable newInter = newObject.AddComponent<Interactable>();
    //    newInter.SetFromDto(inter.dto);

    //    Destroy(inter);
    //}

    //public override void UpdateInteractions(IEnumerable<AbstractInteractionDto> dtos)
    //{
    //    foreach (AbstractInteractionDto dto in dtos)
    //    {
    //        UpdateInteraction(dto);
    //    }
    //}

    //public override void UpdateInteraction(AbstractInteractionDto dto)
    //{
    //    if (!InteractionExists(dto.id))
    //        return;

    //    if (interactionsIdToDto.TryGetValue(dto.id, out AbstractInteractionDto old))
    //    {
    //        if (!old.Equals(dto))
    //        {
    //            interactionsIdToDto.Remove(dto.id);
    //            interactionsIdToDto.Add(dto.id, dto);
    //        }
    //    }
    //    else
    //    {
    //        interactionsIdToDto.Add(dto.id, dto);
    //    }
    //}

    //#endregion

    //#region Delete

    //public override int DeleteInteractions(IEnumerable<string> ids)
    //{
    //    int count = 0;
    //    foreach (string id in ids)
    //    {
    //        if (DeleteInteraction(id))
    //            count++;
    //    }
    //    return count;
    //}

    //public override bool DeleteInteraction(string id)
    //{
    //    if (!InteractionExists(id))
    //        return false;

    //    AbstractInteractionDto dto = GetInteraction(id);

    //    if (!ToolExists(dto.ToolId))
    //        return false;

    //    AbstractTool tool = GetTool(dto.ToolId);

    //    return tool.interactions.Remove(id) && interactionsIdToDto.Remove(id);
    //}

    //public override int DeleteToolboxes(IEnumerable<string> ids)
    //{
    //    int count = 0;
    //    foreach (string id in ids)
    //    {
    //        if (DeleteToolbox(id))
    //            count++;
    //    }
    //    return count;
    //}

    //public override bool DeleteToolbox(string id)
    //{
    //    if (!ToolboxExists(id))
    //        return false;

    //    Toolbox toolbox = GetToolbox(id);
    //    DeleteTools(new List<string>( toolbox.tools));

    //    return toolboxesIdToMenu.Remove(id);
    //}

    //public override int DeleteTools(IEnumerable<string> ids)
    //{
    //    int count = 0;
    //    foreach (string toolId in ids)
    //    {
    //        if (DeleteTool(toolId))
    //            count++;
    //    }
    //    return count;
    //}

    //public override bool DeleteTool(string id)
    //{
    //    if (projectedTools.ContainsKey(id))
    //        ReleaseTool(id);

    //    if (!ToolExists(id))
    //        return false;

    //    bool success = true;
    //    AbstractTool tool = GetTool(id);

    //    if (tool is Tool)
    //    {

    //        Toolbox toolbox = GetToolbox((tool as Tool).toolboxId);
    //        success &= toolbox.tools.Remove(id);

    //        if (toolsIdToMenu.TryGetValue(id, out ToolMenuItem toolMenu))
    //        {
    //            if (toolboxesIdToMenu.TryGetValue(toolbox.id, out ToolboxSubMenu tbsm))
    //            {
    //                success &= tbsm.Remove(toolMenu);
    //            }
    //        }

    //        success &= toolsIdToMenu.Remove(id);

    //    }
    //    else if (tool is Interactable)
    //    {
    //        Destroy(UMI3DBrowser.Scene.GetObject((tool as Interactable).objectId).GetComponents<Interactable>().First(i => i.id.Equals(tool.id)));
    //    }

    //    DeleteInteractions(new List<string>(tool.interactions));

    //    return success;
    //}




    //#endregion

    #endregion
}
