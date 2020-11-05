# FreehandROI
A simple tool to draw freehand regions of interest on an image and generate a binary mask.

When drawing the region of interest, the ROI will be closed/completed after clicking the starting point again.

## NOTES
- A more robust app would cancel closing or shutdown of the app while the mask is being saved
- It would be more obvious to the user to highlight the ROI starting point or change the cursor on mouse over to indicate that it will close/complete the ROI
- Mask generation relies heavily on functionality within System.Windows.Media so I wouldn't be surprised if there are other ways to implement this that improve performance of large ROIs
- A nice feature to improve user experience would be to enable Ctrl+Scroll for zooming in and on the image while maintaining correct scale of ROI
- A more pure MVVM app design would have a separate view model as the DataContext of the MainWindow with properties
to bind to the image Source and OpacityMask
- A nice UX feature would be to enable additional options for path construction, i.e. bezier curve support
