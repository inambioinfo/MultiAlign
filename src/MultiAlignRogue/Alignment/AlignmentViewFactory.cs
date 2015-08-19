﻿namespace MultiAlignRogue.Alignment
{
    using MultiAlignCore.Data.Alignment;

    class AlignmentViewFactory : IAlignmentWindowFactory
    {
        public void CreateNewWindow()
        {

        }
        public void CreateNewWindow(AlignmentData alignment)
        {
            AlignmentView window = new AlignmentView()
            {
                DataContext = new AlignmentViewModel(alignment)
            };
            window.Show();
        }
    }
}
