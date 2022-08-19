using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Buzz.MachineInterface;
using BuzzGUI.Interfaces;
using BuzzGUI.Common;
using ModernSequenceEditor.Interfaces;

namespace WDE.JumpLoop
{
	[MachineDecl(Name = "JumpLoop", ShortName = "JumpLoop", Author = "WDE", MaxTracks = 1)]
	public class JumpLoopMachine : IBuzzMachine, INotifyPropertyChanged, IModernSequencerMachineInterface
	{
        IBuzzMachineHost host;

		public class JumpParams
        {
			public JumpParams(int jumpCount = 0, bool reset = true)
            {
				JumpCount = jumpCount;
				JumpCounter = jumpCount;
				Reset = reset;
			}

			public int JumpCount { get; set; }
			public int JumpCounter { get; set; }
			public bool Reset { get; set; }

		}

		Dictionary<int, JumpParams> JumpRepo = new Dictionary<int, JumpParams>();
		Dictionary<int, JumpParams> HoldRepo = new Dictionary<int, JumpParams>();

		public JumpLoopMachine(IBuzzMachineHost host)
		{
			this.host = host;
			Global.Buzz.PropertyChanged += Buzz_PropertyChanged;
            Global.Buzz.Song.MachineRemoved += Song_MachineRemoved;
		}

        private void Song_MachineRemoved(IMachine obj)
        {
            if (obj == host.Machine)
            {
				Global.Buzz.PropertyChanged -= Buzz_PropertyChanged;
				Global.Buzz.Song.MachineRemoved -= Song_MachineRemoved;
			}
        }

        private void Buzz_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "Playing")
			{
				if (Global.Buzz.Playing)
				{
					ResetCounters();
					JumpLoopEvents.Clear();
				}
			}
		}

		private void ResetCounters()
		{	
			JumpRepo.Clear();
		}

		public enum JumpType
        {
			Tick = 0,
			Beat,
			Pattern
        }


		[ParameterDecl(IsStateless = true, MaxValue = 2, DefValue = 0, ValueDescriptions = new[] { "Tick", "Beat", "Pattern" }, Description = "Define how jump target is calculated (Tick, Beat, Pattern)" )]
		public int Type { get; set; }

		int jump;
		[ParameterDecl(IsStateless = true, MaxValue = 2000, MinValue = 0, DefValue = 0, ValueDescriptions = new string [] { "Infinite" }, Description = "How many times to jump. Resets when play is pressed. If not set or 0 then jump infinitely.")]
		public int Jump { get => jump; set
			{
				jump = value;

				int playPos = Global.Buzz.Song.PlayPosition;
				if (!JumpRepo.ContainsKey(playPos))
					JumpRepo.Add(playPos, new JumpParams(jump));

				JumpParams jp = JumpRepo[playPos];
				if (jp.JumpCount != jump)
                {
					jp.JumpCount = jump;
					jp.JumpCounter = jump;
				}

				if (jump == 0) //Infinite
					JumpRepo[playPos].JumpCounter = int.MaxValue;

			}
		}

		bool reset;
		[ParameterDecl(IsStateless = true, DefValue = true, ValueDescriptions = new[] { "No", "Yes" }, Description = "Reset counter when zero?")]
		public bool Reset
		{
			get => reset; set
			{
				reset = value;
				int playPos = Global.Buzz.Song.PlayPosition;
				if (JumpRepo.ContainsKey(playPos))
				{
					JumpRepo[playPos].Reset = reset;
				}
			}
		}

		int target;
		[ParameterDecl(IsStateless = true, MaxValue = 0x7fff, DefValue = 0, Description = "Jump target")]
		public int Target { get => target; set
            {
				target = value;
				int playPos = Global.Buzz.Song.PlayPosition;

				if (!JumpRepo.ContainsKey(playPos))
                {
					JumpRepo.Add(playPos, new JumpParams(0, true));
				}

				JumpParams jp = JumpRepo[playPos];
				int jumpCounter = jp.JumpCounter;
				if (jumpCounter > 0 || jp.JumpCount == 0)
                {
					string jumpCountStr = jp.JumpCount != 0 ? "" + (jumpCounter - 1) : "Infinite";
					if ((JumpType)Type == JumpType.Tick)
                    {	
						Global.Buzz.Song.PlayPosition = target;
						NotifyCanvases();
						UpdateEvents("Play Position: " + playPos + " | Jump to tick: " + target + " | Reset: " + (jp.Reset == true ? "Yes" : "No") + " | Jump Count: " + jumpCountStr);
					}
                    else if ((JumpType)Type == JumpType.Beat)
					{
						int newTarget = target * host.MasterInfo.TicksPerBeat;
						Global.Buzz.Song.PlayPosition = newTarget;
						NotifyCanvases();
						UpdateEvents("Play Position: " + playPos + " | Jump to beat: " + target + " | Reset: " + (jp.Reset == true ? "Yes" : "No") + " | Jump Count: " + jumpCountStr);
					}
					else if ((JumpType)Type == JumpType.Pattern)
                    {
						int newTarget = GetTargetPattern(target);

						if (newTarget >= 0)
							Global.Buzz.Song.PlayPosition = newTarget;

						NotifyCanvases();
						UpdateEvents("Play Position: " + playPos + " | Jump to pattern: " + target + " | Reset: " + (jp.Reset == true ? "Yes" : "No") + " | Jump Count: " + jumpCountStr);
					}


					if (jp.JumpCount != 0)
						JumpRepo[playPos].JumpCounter = jumpCounter - 1;
				}
				else if (jp.Reset)
                {
					JumpRepo[playPos].JumpCounter = Jump;
					UpdateEvents("Reset! Play Position: " + playPos + " | Jump Count Left: " + Jump);
				}
            }
		}

        private int GetTargetPattern(int target)
        {
			SortedList<int, SequenceEvent> jumpPatterns = new SortedList<int, SequenceEvent>();

			int ret = -1;

            foreach (var seq in host.Machine.Graph.Buzz.Song.Sequences)
				foreach (var eve in seq.Events)
                {
					if (eve.Value.Pattern.Machine == host.Machine)
                    {
						if (!jumpPatterns.ContainsKey(eve.Key))
							jumpPatterns[eve.Key] = eve.Value;
					}
                }

			try
			{
				ret = jumpPatterns.ElementAt(target).Key;
			}
			catch
            {
				
			}
			return ret;
		}

        int hold;
		[ParameterDecl(IsStateless = true, MaxValue = 20000, DefValue = 0, Description = "Hold x ticks.")]
		public int Hold
		{
			get => hold; set
			{
				hold = value;
				int playPos = Global.Buzz.Song.PlayPosition;

				if (!HoldRepo.ContainsKey(playPos))
				{
					HoldRepo.Add(playPos, new JumpParams(hold, true));
				}

				if (HoldRepo.ContainsKey(playPos))
				{
					JumpParams jp = HoldRepo[playPos];
					int holdCount = jp.JumpCounter;
					if (holdCount > 0)
					{	
						Global.Buzz.Song.PlayPosition = playPos;
						NotifyCanvases();
						UpdateEvents("Play Position: " + playPos + " | Hold Count: " + (holdCount - 1));

						HoldRepo[playPos].JumpCounter = holdCount - 1;
					}
					else 
					{
						HoldRepo.Remove(playPos);
					}
				}
			}
		}

		public void UpdateEvents(string str)
        {
			Application.Current.Dispatcher.BeginInvoke((Action)(() =>
			{
				JumpLoopEvents.Add(str);
				if (JumpLoopEvents.Count > 20)
					JumpLoopEvents.RemoveAt(0);
			}));
		}

		public void NotifyCanvases()
		{
			Application.Current.Dispatcher.BeginInvoke((Action)(() =>
			{
				if (PropertyChanged != null) PropertyChanged.Raise(this, "PatternEvent");
			}));
		}

		public void Work() 
		{
			// if (host.MasterInfo.PosInTick == 0 && host.SubTickInfo.PosInSubTick == 0 && Global.Buzz.Playing)
			// {
			//	int playPos = host.Machine.Graph.Buzz.Song.PlayPosition;
            // }
		}

        public Canvas PrepareCanvasForSequencer(IPattern pat, SequencerLayout layout, double tickHeight, int time, double width, double height)
        {
			JumpLoopCanvas ec = null;

			if (pat.Machine == host.Machine)
			{
				ec = new JumpLoopCanvas(this, pat);
				ec.Width = width;
				ec.Height = height;
			}
			return ec;
		}

        // actual machine ends here. the stuff below demonstrates some other features of the api.

        public class State : INotifyPropertyChanged
		{
			/*
			public State() { text = "here is state"; }	// NOTE: parameterless constructor is required by the xml serializer

			string text;
			public string Text 
			{
				get { return text; }
				set
				{
					text = value;
					if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Text"));
					// NOTE: the INotifyPropertyChanged stuff is only used for data binding in the GUI in this demo. it is not required by the serializer.
				}
			}
			*/
			public event PropertyChangedEventHandler PropertyChanged;
		}

		State machineState = new State();
		public State MachineState			// a property called 'MachineState' gets automatically saved in songs and presets
		{
			get { return machineState; }
			set
			{
				machineState = value;
				if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("MachineState"));
			}
		}		
		
		public IEnumerable<IMenuItem> Commands
		{
			get
			{
				yield return new MenuItemVM() 
				{ 
					Text = "About...", 
					Command = new SimpleCommand()
					{
						CanExecuteDelegate = p => true,
						ExecuteDelegate = p => MessageBox.Show(
@"JumpLopp 0.1 (C) WDE 2021

Type jump count (infinte if 0 or not set) and target tick/beat to activate jumping.")
					}
				};
			}
		}


        ObservableCollection<String> jumpLoopEvents = new ObservableCollection<String>();
		public ObservableCollection<String> JumpLoopEvents { get => jumpLoopEvents; }


		public event PropertyChangedEventHandler PropertyChanged;
	}

	public class MachineGUIFactory : IMachineGUIFactory { public IMachineGUI CreateGUI(IMachineGUIHost host) { return new JumpLoopGUI(); } }
	public class JumpLoopGUI : UserControl, IMachineGUI
	{
		IMachine machine;
		JumpLoopMachine jumpLoopMachine;
		ListBox lb;

		public IMachine Machine
		{
			get { return machine; }
			set
			{
				if (machine != null)
				{
					BindingOperations.ClearBinding(lb, ListBox.ItemsSourceProperty);
				}

				machine = value;

				if (machine != null)
				{
					jumpLoopMachine = (JumpLoopMachine)machine.ManagedMachine;
					
					lb.SetBinding(ListBox.ItemsSourceProperty, new Binding("JumpLoopEvents") { Source = jumpLoopMachine, Mode = BindingMode.OneWay });
				}
			}
		}
		
		public JumpLoopGUI()
		{	
			lb = new ListBox() { Height = 400, Margin = new Thickness(0, 0, 0, 4) };

			var sp = new StackPanel();
			sp.Children.Add(lb);
			this.Content = sp;
		}
	}
}
