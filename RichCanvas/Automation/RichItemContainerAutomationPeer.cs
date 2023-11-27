﻿using Newtonsoft.Json;
using RichCanvas.Automation.ControlInformations;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace RichCanvas.Automation
{
    public class RichItemContainerAutomationPeer : SelectorItemAutomationPeer, IValueProvider
    {
        protected RichItemsControl OwnerItemsControl => (RichItemsControl)ItemsControlAutomationPeer.Owner;

        protected RichItemContainer CurrentContainer => (RichItemContainer)OwnerItemsControl.ItemContainerGenerator.ContainerFromItem(Item);

        public string Value => JsonConvert.SerializeObject(new RichItemContainerData
        {
            IsSelected = CurrentContainer.IsSelected,
            ScaleX = CurrentContainer.ScaleTransform.ScaleX,
            ScaleY = CurrentContainer.ScaleTransform.ScaleY,
        });

        public bool IsReadOnly => true;

        public RichItemContainerAutomationPeer(object item, SelectorAutomationPeer itemsControlAutomationPeer) : base(item, itemsControlAutomationPeer)
        {
        }

        public override object GetPattern(PatternInterface patternInterface) => patternInterface switch
        {
            PatternInterface.Value => this,
            _ => base.GetPattern(patternInterface)
        };

        public void SetValue(string value)
        {
            throw new System.NotSupportedException("This control does not allow setting the value.");
        }

        /// <inheritdoc/>
        protected override AutomationControlType GetAutomationControlTypeCore()
            => AutomationControlType.Custom;

        /// <inheritdoc/>
        protected override string GetClassNameCore() => nameof(RichItemContainer);
    }
}