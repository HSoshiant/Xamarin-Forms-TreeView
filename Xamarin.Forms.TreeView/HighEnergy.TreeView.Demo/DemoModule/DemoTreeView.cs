using System;
using System.Diagnostics;
using Xamarin.Forms;
using HighEnergy.Controls;

namespace HighEnergy.TreeView.Demo
{
    public class DemoTreeView : HighEnergy.Controls.TreeView
    {
        DemoTreeViewModel _viewModel;

        public DemoTreeView()
        {
            // these properties have to be set in a specific order, letting us know that we're doing some dumb things with properties and will need to 
            // TODO: fix this later

            _viewModel = new DemoTreeViewModel();

            NodeCreationFactory =
                () => new TreeNodeView
                {
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    VerticalOptions = LayoutOptions.Start,
                    BackgroundColor = Color.Blue
                };

            HeaderCreationFactory = 
                (it) => new DemoTreeCardView {
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    VerticalOptions = LayoutOptions.Start
                };

            //HeaderCreationFactory = 
            //    () =>
            //    {
            //        var result = new DemoTreeCardView
            //        {
            //            HorizontalOptions = LayoutOptions.FillAndExpand,
            //            VerticalOptions = LayoutOptions.Start
            //        };
            //        Debug.WriteLine("HeaderCreationFactory: new DemoTreeCardView");
            //        return result;
            //    };

            BindingContext = _viewModel.MyTree;

        //    _viewModel.InsertRandomNodes();
        }
    }
}