using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using CodeHub.Core.Services;
using System;
using CodeHub.Core.ViewModels.App;
using System.Threading.Tasks;
using ReactiveUI;
using Xamarin.Utilities.Core.Services;
using Xamarin.Utilities.ViewControllers;
using Xamarin.Utilities.DialogElements;
using Xamarin.Utilities.Purchases;

namespace CodeHub.iOS.Views.App
{
    public class UpgradesView : ViewModelDialogViewController<UpgradesViewModel>
    {
        private readonly IFeaturesService _featuresService;
        private readonly INetworkActivityService _networkActivityService;
        private readonly IAlertDialogService _alertDialogService;
        private readonly List<Item> _items = new List<Item>();

        public UpgradesView(IFeaturesService featuresService, INetworkActivityService networkActivityService, IAlertDialogService alertDialogService)
            : base(style: MonoTouch.UIKit.UITableViewStyle.Plain)
        {
            _featuresService = featuresService;
            _networkActivityService = networkActivityService;
            _alertDialogService = alertDialogService;
        }

        public override void ViewDidLoad()
        {
            Title = "Upgrades";
            NavigationItem.RightBarButtonItem = new MonoTouch.UIKit.UIBarButtonItem("Restore", MonoTouch.UIKit.UIBarButtonItemStyle.Plain, (s, e) => Restore());

            base.ViewDidLoad();

            ViewModel.WhenAnyValue(x => x.Keys).Where(x => x != null && x.Length > 0).Subscribe(x => LoadProducts(x));
        }

        private async Task LoadProducts(string[] keys)
        {
            try
            {
                _networkActivityService.PushNetworkActive();
                var data = await InAppPurchases.Instance.RequestProductData(keys);
                _items.Clear();
                _items.AddRange(data.Products.Select(x => new Item { Id = x.ProductIdentifier, Name = x.LocalizedTitle, Description = x.LocalizedDescription, Price = x.LocalizedPrice() }));
                Render();
            }
            catch (Exception e)
            {
                _alertDialogService.Alert("Error", e.Message);
            }
            finally
            {
                _networkActivityService.PopNetworkActive();
            }
        }

        private void Render()
        {
            var section = new Section();
            section.AddAll(_items.Select(item =>
            {
                var el = new MultilinedElement(item.Name + " (" + item.Price + ")", item.Description);
                if (_featuresService.IsActivated(item.Id))
                {
                    el.Accessory = MonoTouch.UIKit.UITableViewCellAccessory.Checkmark;
                }
                else
                {
                    el.Accessory = MonoTouch.UIKit.UITableViewCellAccessory.DisclosureIndicator;
                    el.Tapped += () => Tapped(item);
                }

                return el;
            }));

            Root.Reset(section);
        }

        private void Restore()
        {
            InAppPurchases.Instance.Restore();
        }

        private void Tapped(Item item)
        {
            _featuresService.Activate(item.Id);
        }

        private class Item
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Price { get; set; }
        }
    }
}

