﻿
using System.Collections.Generic;
using umi3d.cdk.menu.view;
using Unity.UIElements.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace BrowserDesktop.Menu
{
    public class SimpleUIContainer2D : AbstractMenuDisplayContainer, IDisplayerElement
    {
        #region Fields

        #region UIFields

        public PanelRenderer panelRenderer;

        [Tooltip("UXML tage name of the select")]
        public int spaceBetweenElements = 10;

        [Tooltip("This container will be instantiated in this umxl tag")]
        public string uxmlParentTag;
        [Tooltip("UXML name of the tag where the elements will be instanciated")]
        public string uxmlContentTag;
        [Tooltip("UXML tage name of the back button")]
        public string uxmlSelectButtonTag = "container-select-btn";
        [Tooltip("UXML tage name of the select")]
        public string uxmlBackButtonTag = "container-back-btn";

        public VisualTreeAsset containerTreeAsset;

        protected VisualElement containerElement;
        protected VisualElement contentElement;
        private Button backButton;
        private Button selectButton;

        #endregion

        #region Data Fields

        private List<AbstractDisplayer> containedDisplayers = new List<AbstractDisplayer>();

        private AbstractMenuDisplayContainer virtualContainer;

        protected AbstractMenuDisplayContainer VirtualContainer
        {
            get => virtualContainer; set
            {
                if (virtualContainer != null && backButton != null)
                    backButton.clickable.clicked -= virtualContainer.backButtonPressed.Invoke;
                virtualContainer = value;

                bool display = virtualContainer?.parent != null;
              
                if (backButton != null && (backButton.resolvedStyle.display == DisplayStyle.Flex) != display)
                {
                    backButton.style.display = display ? DisplayStyle.Flex : DisplayStyle.None;
                }
                if (display && backButton!= null)
                {
                    backButton.clickable.clicked += virtualContainer.backButtonPressed.Invoke;
                }
            }
        }

        #endregion

        #endregion

        #region Methods

        private void Awake()
        {
            gameObject.SetActive(true);
        }

        public void InitAndBindUI()
        {
            if(containerElement == null)
            {
                containerElement = containerTreeAsset.CloneTree();
                panelRenderer.visualTree.Q<VisualElement>(uxmlParentTag).Add(containerElement);

                BindUI();
            }
        }

        protected virtual void BindUI()
        {
            contentElement = containerElement.Q<VisualElement>(uxmlContentTag);
            selectButton = containerElement.Q<Button>(uxmlSelectButtonTag);
            backButton = containerElement.Q<Button>(uxmlBackButtonTag);
            Debug.Assert(contentElement != null);
            Debug.Assert(selectButton != null);
            Debug.Assert(backButton != null);
        }

        public override AbstractDisplayer this[int i] { get => containedDisplayers[i]; set { RemoveAt(i); Insert(value, i); } }

        void HideBackButton()
        {
            if (backButton != null)
                backButton.style.display = DisplayStyle.None;

            if (virtualContainer != null && backButton != null)
                backButton.clickable.clicked -= virtualContainer.backButtonPressed.Invoke;
        }

        public override void Clear()
        {
            base.Clear();
            foreach (var displayer in containedDisplayers)
                displayer.Clear();
            HideBackButton();

            if (selectButton != null)
                selectButton.clickable.clicked -= Select;

            contentElement.Clear();

            RemoveAll();
        }

        public override bool Contains(AbstractDisplayer element)
        {
            return containedDisplayers.Contains(element);
        }

        public override void Display(bool forceUpdate = false)
        {
            InitAndBindUI();

            if (isDisplayed && !forceUpdate)
            {
                return;
            }

            if (selectButton != null)
            {
                selectButton.clickable.clicked += Select;
                selectButton.text = menu.Name;
            }

            contentElement.style.display = DisplayStyle.Flex;

            if (VirtualContainer != null) VirtualContainer = this;

            foreach (AbstractDisplayer disp in containedDisplayers)
            {
                if (disp is IDisplayerElement)
                    disp.Display();
            }
            isDisplayed = true;
        }

        public override int GetIndexOf(AbstractDisplayer element)
        {
            if (element is IDisplayerElement elt)
            {
                VisualElement umxlElt = elt.GetUXMLContent();
                if (umxlElt.parent != null)
                    return umxlElt.parent.IndexOf(umxlElt);
            }
            else
            {
                throw new System.NotImplementedException("This container is only made to work with IDisplayerElement");
            }

            return element.transform.GetSiblingIndex();
        }

        protected override IEnumerable<AbstractDisplayer> GetDisplayers()
        {
            return containedDisplayers;
        }

        //public override IEnumerable<AbstractMenuDisplayContainer> GetSubMenuDisplayContainers()
        //{
        //    List<AbstractMenuDisplayContainer> container = new List<AbstractMenuDisplayContainer>();
        //    foreach(var displayer in containedDisplayers)
        //    {
        //        if (displayer is AbstractMenuDisplayContainer)
        //            container.Add(displayer as AbstractMenuDisplayContainer);
        //    }
        //    return container;
        //}

        public override void Hide()
        {
            foreach (AbstractDisplayer disp in containedDisplayers)
            {
                disp.Hide();
            }
            HideBackButton();
            if (selectButton != null)
                selectButton.clickable.clicked -= Select;
            contentElement.style.display = DisplayStyle.None;
            isDisplayed = false;
        }

        /// <summary>
        /// Insert an element in the display container.
        /// </summary>
        /// <param name="element">Element to insert</param>
        /// <param name="updateDisplay">Should update the display (default is true)</param>
        public override void Insert(AbstractDisplayer element, bool updateDisplay = true)
        {
            InitAndBindUI();

            if (element is IDisplayerElement elt)
            {
                var uxmlContent = elt.GetUXMLContent();
                uxmlContent.style.marginBottom = uxmlContent.resolvedStyle.marginBottom + spaceBetweenElements;
                contentElement.Add(elt.GetUXMLContent());
                containedDisplayers.Add(element);

                element.transform.SetParent(this.transform, false);

                if (updateDisplay)
                    Display();
            }
            else
            {
                throw new System.NotImplementedException("This container is only made to work with IDisplayerElement. " + element.GetType().ToString() + " displayer given");
            }
        }

        /// <summary>
        /// Insert a collection of elements in the display container.
        /// </summary>
        /// <param name="elements">Elements to insert</param>
        /// <param name="updateDisplay">Should update the display (default is true)</param>
        public override void InsertRange(IEnumerable<AbstractDisplayer> elements, bool updateDisplay = true)
        {
            foreach (AbstractDisplayer e in elements)
            {
                Insert(e, false);
            }
            if (updateDisplay)
                Display();
        }

        public override void Insert(AbstractDisplayer element, int index, bool updateDisplay = true)
        {
            if (element is IDisplayerElement elt)
            {
                contentElement.Insert(index, elt.GetUXMLContent());

                element.transform.SetParent(this.transform, false);
                element.transform.SetSiblingIndex(index);

                containedDisplayers.Add(element);
                if (updateDisplay)
                    Display();
            }
            else
            {
                throw new System.NotImplementedException("This container is only made to work with IDisplayerElement");
            }
        }

        /// <summary>
        /// Remove an object from the display container.
        /// </summary>
        /// <param name="element">Element to remove</param>
        /// <param name="updateDisplay">Should update the display (default is true)</param>
        public override bool Remove(AbstractDisplayer element, bool updateDisplay = true)
        {
            if (element == null) return false;
            bool ok = containedDisplayers.Remove(element);
            if (updateDisplay)
                Display();
            return ok;
        }

        /// <summary>
        /// Remove all elements from the display container.
        /// </summary>
        public override int RemoveAll()
        {
            List<AbstractDisplayer> elements = new List<AbstractDisplayer>(containedDisplayers);
            int count = 0;
            foreach (AbstractDisplayer element in elements)
                if (Remove(element, false)) count++;
            return count;
        }

        public override bool RemoveAt(int index, bool updateDisplay = true)
        {
            return Remove(containedDisplayers?[index], updateDisplay);
        }

        public override void Expand(bool forceUpdate = false)
        {
            if (!isDisplayed)
            {
                Display(forceUpdate);
            }
            if (isExpanded && !forceUpdate)
            {
                return;
            }
            
            if (VirtualContainer != null && VirtualContainer != this)
            {
                selectButton.text = menu.Name;
                foreach (AbstractDisplayer displayer in VirtualContainer)
                {
                    if (displayer is IDisplayerElement elt)
                    {
                        displayer.Hide();
                        (VirtualContainer as SimpleUIContainer2D)?.contentElement.Add(elt.GetUXMLContent());
                        displayer.transform.SetParent((VirtualContainer as SimpleUIContainer2D)?.transform);
                    }
                    else
                    {
                        throw new System.NotImplementedException("This container is only made to work with IDisplayerElement");
                    }
                }
            }
            contentElement.style.display = DisplayStyle.Flex;

            VirtualContainer = this;

            selectButton.clickable.clicked -= Select;

            foreach (AbstractDisplayer displayer in this)
            {
                if (displayer is IDisplayerElement elt)
                    displayer.Display();
                else
                    throw new System.NotImplementedException("This container is only made to work with IDisplayerElement");
            }

            isExpanded = true;
        }

        public override void Collapse(bool forceUpdate = false)
        {
            if (!isExpanded && !forceUpdate)
            {
                return;
            }
            if (VirtualContainer != null && VirtualContainer != this)
            {
                selectButton.text = menu.Name;
                foreach (AbstractDisplayer displayer in VirtualContainer)
                {
                    if (displayer is IDisplayerElement elt)
                    {
                        displayer.Hide();
                        (VirtualContainer as SimpleUIContainer2D)?.contentElement.Add(elt.GetUXMLContent());
                    }
                    else
                    {
                        throw new System.NotImplementedException("This container is only made to work with IDisplayerElement");
                    }
                }
                VirtualContainer = this;
            }

            HideBackButton();
            contentElement.style.display = DisplayStyle.None;
            if (backButton != null)
                backButton.style.display = DisplayStyle.None;
            if (selectButton != null)
                selectButton.clickable.clicked += Select;

            foreach (AbstractDisplayer displayer in this)
            {
                if (displayer is IDisplayerElement)
                    displayer.Hide();
            }
            isExpanded = false;
        }

        public override void ExpandAs(AbstractMenuDisplayContainer Container, bool forceUpdate = false)
        {
            if (isExpanded && !forceUpdate)
            {
                return;
            }
            if (VirtualContainer != null && VirtualContainer != Container)
            {
                if (backButton != null)
                    backButton.style.display = DisplayStyle.None;
                if (selectButton != null)
                    selectButton.clickable.clicked += Select;


                foreach (AbstractDisplayer displayer in this)
                {
                    if (displayer is IDisplayerElement)
                        displayer.Hide();
                }
            }

            VirtualContainer = Container;
            selectButton.text = Container.menu.Name;
            contentElement.style.display = DisplayStyle.Flex;

            if (selectButton != null)
                selectButton.clickable.clicked -= Select;

            foreach (AbstractDisplayer displayer in VirtualContainer)
            {
                if (displayer is IDisplayerElement elt)
                {
                    contentElement.Add(elt.GetUXMLContent());
                    displayer.transform.SetParent(this.transform);
                    displayer.Display();
                }
            }
            isExpanded = true;
        }

        public override AbstractMenuDisplayContainer CurrentMenuDisplayContainer()
        {
            return VirtualContainer;
        }

        public override int IsSuitableFor(umi3d.cdk.menu.AbstractMenuItem menu)
        {
            return (menu is umi3d.cdk.menu.Menu) ? 1 : 0;
        }

        public override int Count()
        {
            return containedDisplayers.Count;
        }

        public VisualElement GetUXMLContent()
        {
            return containerElement;
        }

        /*public void OnDestroy()
        {
            containerElement?.RemoveFromHierarchy();
        }*/
        #endregion
    }
}