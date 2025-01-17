﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using Microsoft.Kinect;
using LinearAlgebra;
using System.ComponentModel;

namespace PoseDetector
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		/// <summary>
		/// Radius of drawn hand circles
		/// </summary>
		private const double HandSize = 30;

		/// <summary>
		/// Thickness of drawn joint lines
		/// </summary>
		private const double JointThickness = 3;

		/// <summary>
		/// Thickness of clip edge rectangles
		/// </summary>
		private const double ClipBoundsThickness = 10;

		/// <summary>
		/// Constant for clamping Z values of camera space points from being negative
		/// </summary>
		private const float InferredZPositionClamp = 0.1f;

		/// <summary>
		/// Brush used for drawing hands that are currently tracked as closed
		/// </summary>
		private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

		/// <summary>
		/// Brush used for drawing hands that are currently tracked as opened
		/// </summary>
		private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

		/// <summary>
		/// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
		/// </summary>
		private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

		/// <summary>
		/// Brush used for drawing joints that are currently tracked
		/// </summary>
		private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

		/// <summary>
		/// Brush used for drawing joints that are currently inferred
		/// </summary>        
		private readonly Brush inferredJointBrush = Brushes.Yellow;

		/// <summary>
		/// Pen used for drawing bones that are currently inferred
		/// </summary>        
		private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

		/// <summary>
		/// Drawing group for body rendering output
		/// </summary>
		private DrawingGroup drawingGroup;

		/// <summary>
		/// Drawing image that we will display
		/// </summary>
		private DrawingImage imageSource;

		/// <summary>
		/// Active Kinect sensor
		/// </summary>
		private KinectSensor kinectSensor = null;

		/// <summary>
		/// Coordinate mapper to map one type of point to another
		/// </summary>
		private CoordinateMapper coordinateMapper = null;

		/// <summary>
		/// Reader for body frames
		/// </summary>
		private BodyFrameReader bodyFrameReader = null;

		/// <summary>
		/// Array for the bodies
		/// </summary>
		private Body[] bodies = null;

		/// <summary>
		/// definition of bones
		/// </summary>
		private static List<Tuple<JointType, JointType>> bones;

		/// <summary>
		/// Width of display (depth space)
		/// </summary>
		private int displayWidth;

		/// <summary>
		/// Height of display (depth space)
		/// </summary>
		private int displayHeight;

		/// <summary>
		/// List of colors for each body tracked
		/// </summary>
		private List<Pen> bodyColors;

		/// <summary>
		/// Current status text to display
		/// </summary>
		private string statusText = null;

		public TextBlock[,] jointInfo = new TextBlock[25, 5];

		public TextBlock[,] boneInfo = new TextBlock[25, 5];

		/// <summary>
		/// Initializes a new instance of the MainWindow class.
		/// </summary>
		public MainWindow()
		{
			// initialize the components (controls) of the window
			this.InitializeComponent();

			// one sensor is currently supported
			this.kinectSensor = KinectSensor.GetDefault();

			// get the coordinate mapper
			this.coordinateMapper = this.kinectSensor.CoordinateMapper;

			// get the depth (display) extents
			FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

			// get size of joint space
			this.displayWidth = frameDescription.Width;
			this.displayHeight = frameDescription.Height;

			// open the reader for the body frames
			this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

			// a bone defined as a line between two joints
			bones = new List<Tuple<JointType, JointType>>();

			// Torso
			bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
			bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
			bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
			bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
			bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
			bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
			bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
			bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

			// Right Arm
			bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
			bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
			bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
			bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
			bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

			// Left Arm
			bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
			bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
			bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
			bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
			bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

			// Right Leg
			bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
			bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
			bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

			// Left Leg
			bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
			bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
			bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

			// populate body colors, one for each BodyIndex
			this.bodyColors = new List<Pen>();

			this.bodyColors.Add(new Pen(Brushes.Red, 6));
			this.bodyColors.Add(new Pen(Brushes.Orange, 6));
			this.bodyColors.Add(new Pen(Brushes.Green, 6));
			this.bodyColors.Add(new Pen(Brushes.Blue, 6));
			this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
			this.bodyColors.Add(new Pen(Brushes.Violet, 6));

			// set IsAvailableChanged event notifier
			this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

			// open the sensor
			this.kinectSensor.Open();

			// set the status text
			this.StatusText = this.kinectSensor.IsAvailable ? "Running"
															: "Kinect unavailable";
			startButton.IsEnabled = this.kinectSensor.IsAvailable;

			// Create the drawing group we'll use for drawing
			this.drawingGroup = new DrawingGroup();

			// Create an image source that we can use in our image control
			this.imageSource = new DrawingImage(this.drawingGroup);

			// use the window object as the view model in this simple example
			this.DataContext = this;

			for (int i = 0; i < 25; i++)
			{
				for (int j = 0; j < 5; j++)
				{
					jointInfo[i, j] = new TextBlock();
					jointsDisplayPanel.Children.Add(jointInfo[i, j]);
					jointInfo[i, j].SetValue(Grid.RowProperty, i + 1);
					jointInfo[i, j].SetValue(Grid.ColumnProperty, j);

					boneInfo[i, j] = new TextBlock();
					bonesDisplayPanel.Children.Add(boneInfo[i, j]);
					boneInfo[i, j].SetValue(Grid.RowProperty, i + 1);
					boneInfo[i, j].SetValue(Grid.ColumnProperty, j);
				}
			}
		}

		/// <summary>
		/// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Gets the bitmap to display
		/// </summary>
		public ImageSource ImageSource
		{
			get
			{
				return this.imageSource;
			}
		}

		/// <summary>
		/// Gets or sets the current status text to display
		/// </summary>
		public string StatusText
		{
			get
			{
				return this.statusText;
			}

			set
			{
				if (this.statusText != value)
				{
					this.statusText = value;

					// notify any bound elements that the text has changed
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("StatusText"));
				}
			}
		}

		/// <summary>
		/// Execute start up tasks
		/// </summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void MainWindow_Loaded(object sender, RoutedEventArgs e)
		{
			if (this.bodyFrameReader != null)
			{
				this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
			}
		}

		/// <summary>
		/// Execute shutdown tasks
		/// </summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void MainWindow_Closing(object sender, CancelEventArgs e)
		{
			if (this.bodyFrameReader != null)
			{
				// BodyFrameReader is IDisposable
				this.bodyFrameReader.Dispose();
				this.bodyFrameReader = null;
			}

			if (this.kinectSensor != null)
			{
				this.kinectSensor.Close();
				this.kinectSensor = null;
			}
		}

		/// <summary>
		/// Handles the body frame data arriving from the sensor
		/// </summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
		{
			bool dataReceived = false;

			using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
			{
				if (bodyFrame != null)
				{
					if (this.bodies == null)
					{
						this.bodies = new Body[bodyFrame.BodyCount];
					}

					// The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
					// As long as those body objects are not disposed and not set to null in the array,
					// those body objects will be re-used.
					bodyFrame.GetAndRefreshBodyData(this.bodies);
					dataReceived = true;
				}
			}

			if (dataReceived)
			{
				using (DrawingContext dc = this.drawingGroup.Open())
				{
					// Draw a transparent background to set the render size
					dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

					int penIndex = 0;
					Pen drawPen = null;
					Body body = null;
					foreach (Body currentBody in this.bodies)
					{
						drawPen = this.bodyColors[penIndex++];

						if (currentBody.IsTracked)
						{
							body = currentBody;
						}
					}

					if (body != null && drawPen != null)
					{
						this.DrawClippedEdges(body, dc);

						IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

						// convert the joint points to depth (display) space
						Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

						int row = 0;

						foreach (var bone in bones)
						{
							Joint joint1 = joints[bone.Item1];
							Joint joint2 = joints[bone.Item2];
							boneInfo[row, 0].Text = $"{bone.Item1}";
							boneInfo[row, 1].Text = $"{bone.Item2}";
							if ((joint1.TrackingState == TrackingState.Tracked) && (joint2.TrackingState == TrackingState.Tracked))
							{
								CameraSpacePoint position1 = joint1.Position;
								CameraSpacePoint position2 = joint2.Position;
								ColumnVector vector = ColumnVector.Create(position1.X - position2.X, position1.Y - position2.Y, position1.Z - position2.Z);
								boneInfo[row, 2].Text = $"{vector[1]: #0.000000}";
								boneInfo[row, 3].Text = $"{vector[2]: #0.000000}";
								boneInfo[row, 4].Text = $"{vector[3]: #0.000000}";
								if (Segment.isRecording)
								{
									Segment.poseDoc.Add(row, vector);
								}
							}
							else
							{
								for (int i = 2; i < 5; i++)
								{
									boneInfo[row, i].Text = "Not Tracked";
								}
								if (Segment.isRecording)
								{
									Segment.poseDoc.Add(row, null);
								}
							}
							row++;
						}

						row = 0;

						foreach (JointType jointType in joints.Keys)
						{
							// sometimes the depth(Z) of an inferred joint may show as negative
							// clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
							CameraSpacePoint position = joints[jointType].Position;

							jointInfo[row, 0].Text = $"{Convert.ToInt32(jointType) + 1}. {jointType}";
							jointInfo[row, 1].Text = $"{joints[jointType].TrackingState}";
							jointInfo[row, 2].Text = position.X.ToString("#0.000000");
							jointInfo[row, 3].Text = position.Y.ToString("#0.000000");
							jointInfo[row, 4].Text = position.Z.ToString("#0.000000");

							row++;
							if (position.Z < 0)
							{
								position.Z = InferredZPositionClamp;
							}

							DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
							jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
						}

						this.DrawBody(joints, jointPoints, dc, drawPen);

						this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
						this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);
					}

					// prevent drawing outside of our render area
					this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
				}
			}
		}

		/// <summary>
		/// Draws a body
		/// </summary>
		/// <param name="joints">joints to draw</param>
		/// <param name="jointPoints">translated positions of joints to draw</param>
		/// <param name="drawingContext">drawing context to draw to</param>
		/// <param name="drawingPen">specifies color to draw a specific body</param>
		private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
		{
			// Draw the bones
			foreach (var bone in bones)
			{
				this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
			}

			// Draw the joints
			foreach (JointType jointType in joints.Keys)
			{
				Brush drawBrush = null;

				TrackingState trackingState = joints[jointType].TrackingState;

				if (trackingState == TrackingState.Tracked)
				{
					drawBrush = this.trackedJointBrush;
				}
				else if (trackingState == TrackingState.Inferred)
				{
					drawBrush = this.inferredJointBrush;
				}

				if (drawBrush != null)
				{
					drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
				}
			}
		}

		/// <summary>
		/// Draws one bone of a body (joint to joint)
		/// </summary>
		/// <param name="joints">joints to draw</param>
		/// <param name="jointPoints">translated positions of joints to draw</param>
		/// <param name="jointType0">first joint of bone to draw</param>
		/// <param name="jointType1">second joint of bone to draw</param>
		/// <param name="drawingContext">drawing context to draw to</param>
		/// /// <param name="drawingPen">specifies color to draw a specific bone</param>
		private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
		{
			//bool isTracked = false;

			Joint joint0 = joints[jointType0];
			Joint joint1 = joints[jointType1];

			// If we can't find either of these joints, exit
			if (joint0.TrackingState == TrackingState.NotTracked ||
				joint1.TrackingState == TrackingState.NotTracked)
			{
				return;
			}

			// We assume all drawn bones are inferred unless BOTH joints are tracked
			Pen drawPen = this.inferredBonePen;
			if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
			{
				drawPen = drawingPen;
				//isTracked = true;
			}

			drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);

		}

		/// <summary>
		/// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
		/// </summary>
		/// <param name="handState">state of the hand</param>
		/// <param name="handPosition">position of the hand</param>
		/// <param name="drawingContext">drawing context to draw to</param>
		private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
		{
			switch (handState)
			{
				case HandState.Closed:
					drawingContext.DrawEllipse(this.handClosedBrush, null, handPosition, HandSize, HandSize);
					break;

				case HandState.Open:
					drawingContext.DrawEllipse(this.handOpenBrush, null, handPosition, HandSize, HandSize);
					break;

				case HandState.Lasso:
					drawingContext.DrawEllipse(this.handLassoBrush, null, handPosition, HandSize, HandSize);
					break;
			}
		}

		/// <summary>
		/// Draws indicators to show which edges are clipping body data
		/// </summary>
		/// <param name="body">body to draw clipping information for</param>
		/// <param name="drawingContext">drawing context to draw to</param>
		private void DrawClippedEdges(Body body, DrawingContext drawingContext)
		{
			FrameEdges clippedEdges = body.ClippedEdges;

			if (clippedEdges.HasFlag(FrameEdges.Bottom))
			{
				drawingContext.DrawRectangle(
					Brushes.Red,
					null,
					new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
			}

			if (clippedEdges.HasFlag(FrameEdges.Top))
			{
				drawingContext.DrawRectangle(
					Brushes.Red,
					null,
					new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
			}

			if (clippedEdges.HasFlag(FrameEdges.Left))
			{
				drawingContext.DrawRectangle(
					Brushes.Red,
					null,
					new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
			}

			if (clippedEdges.HasFlag(FrameEdges.Right))
			{
				drawingContext.DrawRectangle(
					Brushes.Red,
					null,
					new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
			}
		}

		/// <summary>
		/// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
		/// </summary>
		/// <param name="sender">object sending the event</param>
		/// <param name="e">event arguments</param>
		private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
		{
			// on failure, set the status text
			this.StatusText = this.kinectSensor.IsAvailable ? "Running"
															: "Kinect unavailable";

			startButton.IsEnabled = this.kinectSensor.IsAvailable;
		}

		public static class Segment
		{
			public class PoseArchive
			{
				private string defaultPath = $@"D:\Personal Files\OneDrive - bupt.edu.cn\Kinect Docs\Archives\KinectPoseArchive_{DateTime.Now:yyyy-MM-dd_HH.mm.ss}.xml";

				private XmlDocument xmlDocument;

				private XmlElement rootElement;

				private XmlElement[] boneElement;

				private DateTime startDT;

				public PoseArchive()
				{
					xmlDocument = new XmlDocument();
					rootElement = xmlDocument.CreateElement("Pose");
					xmlDocument.AppendChild(rootElement);
					boneElement = new XmlElement[24];
					for (int i = 0; i < 24; i++)
					{
						boneElement[i] = xmlDocument.CreateElement("Bone");
						rootElement.AppendChild(boneElement[i]);
						boneElement[i].SetAttribute("To", $"{bones[i].Item1}");
						boneElement[i].SetAttribute("From", $"{bones[i].Item2}");
					}
					startDT = DateTime.Now;
				}

				public void Add(int index, ColumnVector vector)
				{
					XmlElement xmlElement = xmlDocument.CreateElement($"Node");
					boneElement[index].AppendChild(xmlElement);
					xmlElement.SetAttribute("Time", $"{(DateTime.Now - startDT).TotalMilliseconds: #0.}");
					if (vector != null)
					{
						if (vector.IsZeroMatrix())
						{
							xmlElement.InnerText = "null";
						}
						else
						{
							vector = vector.Normalize();
							vector[1] = Math.Round(vector[1] * 100, 0);
							vector[2] = Math.Round(vector[2] * 100, 0);
							vector[3] = Math.Round(vector[3] * 100, 0);
							xmlElement.InnerText = vector.ToString();
						}
					}
					else
					{
						xmlElement.InnerText = "null";
					}
				}

				public void Save()
				{
					xmlDocument.Save(defaultPath);
				}
			}

			public static PoseArchive poseDoc = null;

			public static bool isRecording = false;
		}

		private void startButton_Click(object sender, RoutedEventArgs e)
		{
			endButton.IsEnabled = true;
			startButton.IsEnabled = false;
			tabItem1.Foreground = new SolidColorBrush(Colors.Blue);
			tabItem2.Foreground = new SolidColorBrush(Colors.Blue);
			Segment.poseDoc = new Segment.PoseArchive();
			Segment.isRecording = true;
		}

		private void endButton_Click(object sender, RoutedEventArgs e)
		{
			Segment.isRecording = false;
			endButton.IsEnabled = false;
			startButton.IsEnabled = true;
			tabItem1.Foreground = new SolidColorBrush(Colors.Black);
			tabItem2.Foreground = new SolidColorBrush(Colors.Black);
			Segment.poseDoc.Save();
			Segment.poseDoc = null;
			MessageBox.Show("原始存档已保存。");
		}
	}
}
