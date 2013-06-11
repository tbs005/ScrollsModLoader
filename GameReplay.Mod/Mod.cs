using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
using Mono.Cecil;
using UnityEngine;
using ScrollsModLoader.Interfaces;

namespace GameReplay.Mod
{
	public class Mod : BaseMod, ICommListener, IListCallback
	{
		private Recorder recorder;
		private Player player;
		private UIListPopup recordListPopup;
		List<Item> recordList = new List<Item>();

		public Mod ()
		{
			if (!Directory.Exists (this.OwnFolder()+Path.DirectorySeparatorChar+"Records"+Path.DirectorySeparatorChar))
				Directory.CreateDirectory (this.OwnFolder()+Path.DirectorySeparatorChar+"Records"+Path.DirectorySeparatorChar);
		}

		public override string GetName ()
		{
			return "GameReplay";
		}

		public override int GetVersion ()
		{
			return 1;
		}

		public override void Init ()
		{
			player = new Player (this.OwnFolder()+Path.DirectorySeparatorChar+"Records");
			App.Communicator.addListener (this);
		}


		public void handleMessage (Message msg)
		{
			if (msg is BattleRedirectMessage) {
				recorder = new Recorder (this.OwnFolder()+Path.DirectorySeparatorChar+"Records");
			}
		}

		public void onReconnect ()
		{
			return;
		}

		public override MethodDefinition[] GetHooks (TypeDefinitionCollection scrollsTypes, int version)
		{
			MethodDefinition[] defs = new MethodDefinition[] {
				scrollsTypes["ProfileMenu"].Methods.GetMethod("Start")[0],
				scrollsTypes["ProfileMenu"].Methods.GetMethod("getButtonRect")[0],
				scrollsTypes["ProfileMenu"].Methods.GetMethod("drawEditButton")[0]
			};

			List<MethodDefinition> list = new List<MethodDefinition>(defs);
			list.AddRange(Player.GetPlayerHooks (scrollsTypes, version));

			return list.ToArray();
		}

		public override bool BeforeInvoke (InvocationInfo info, out object returnValue)
		{
			if (info.TargetMethod ().Equals ("getButtonRect")) {
				foreach (StackFrame frame in info.StackTrace().GetFrames()) {
					if (frame.GetMethod ().Name.Contains ("drawEditButton")) {
						returnValue = typeof(ProfileMenu).GetMethod ("getButtonRect", BindingFlags.NonPublic | BindingFlags.Instance).Invoke (info.Target(), new object[] { 0 });
						return true;
					}
				}
				returnValue = null;
				return false;
			}
			if (info.Target () is ProfileMenu && info.TargetMethod().Equals("Start")) {

				//list them
				recordList.Clear ();
				foreach (String file in Directory.GetFiles(this.OwnFolder()+Path.DirectorySeparatorChar+"Records")) {
					if (file.EndsWith("sgr")) {

						JsonMessageSplitter jsonms = new JsonMessageSplitter ();
						String log = File.ReadAllText (file);
						jsonms.feed(log);
						jsonms.runParsing();
						String line = jsonms.getNextMessage ();
						bool searching = true;
						String enemyName = null;
						String deckName = null;
						ResourceType type = ResourceType.NONE;

						while (line != null && searching) {
							try {
								Message msg = Message.createMessage (Message.getMessageName(line), line);
								if (msg is GameInfoMessage) {
									if ((msg as GameInfoMessage).white.Equals(App.Communicator.getUserScreenName()))
										enemyName = (msg as GameInfoMessage).black;
									else
										enemyName = (msg as GameInfoMessage).white;
									deckName = (msg as GameInfoMessage).deck;
								}
								if (msg is ActiveResourcesMessage) {
									type = (msg as ActiveResourcesMessage).types[0];
								}
								if (enemyName != null && deckName != null && type != ResourceType.NONE)
									searching = false;
							} catch {}
							jsonms.runParsing();
							line = jsonms.getNextMessage ();
						}

						recordList.Add (new Record(File.GetCreationTime(file).ToLongDateString()+" - "+File.GetCreationTime(file).ToLongTimeString(), "VS "+enemyName+" - "+deckName, file, type));
					}
				}

				recordListPopup = new GameObject ("Record List").AddComponent<UIListPopup> ();
				recordListPopup.transform.parent = ((ProfileMenu)info.Target()).transform;
				Rect frame = (Rect)typeof(ProfileMenu).GetField ("frameRect", BindingFlags.Instance | BindingFlags.NonPublic).GetValue (info.Target());
				recordListPopup.Init (new Rect(frame.center.x-frame.width/2.0f, frame.center.y-frame.height/2.0f+32.0f, frame.width, frame.height-(float)Screen.height*0.055f*3.0f-42.0f), false, true, recordList, this, null, null, false, true, false, true, null, true, false);
				recordListPopup.enabled = true;
				recordListPopup.SetOpacity(1f);
			}
			if (info.TargetMethod().Equals("drawEditButton")) {
				Rect rect = (Rect)typeof(ProfileMenu).GetField ("frameRect", BindingFlags.Instance | BindingFlags.NonPublic).GetValue (info.Target());
				//LobbyMenu.drawShadowText (new Rect(rect.center.x-(float)Screen.width/8.0f/2.0f, rect.center.y-rect.height/2.0f-(float)Screen.height*0.055f*3.0f-40.0f, (float)Screen.width/8.0f, 35.0f), "Match History", Color.white);

				if ((bool)typeof(ProfileMenu).GetField ("showEdit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue (info.Target())) {
					recordListPopup.enabled = false;
					recordListPopup.SetOpacity(0f);
				} else {
					new ScrollsFrame (new Rect(rect.center.x-rect.width/2.0f-20.0f, rect.center.y-rect.height/2.0f, rect.width+40.0f, rect.height-(float)Screen.height*0.055f*3.0f-20.0f)).AddNinePatch (ScrollsFrame.Border.LIGHT_CURVED, NinePatch.Patches.CENTER).Draw ();
					recordListPopup.enabled = true;
					recordListPopup.SetOpacity(1f);
					GUIStyle labelSkin = (GUIStyle)typeof(ProfileMenu).GetField ("usernameStyle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue (info.Target());
					labelSkin.fontSize = 32;
					GUI.Label (new Rect(rect.center.x-(float)Screen.width/6.0f/2.0f, rect.center.y-rect.height/2.0f-40.0f, (float)Screen.width/6.0f, 35.0f), "Match History", labelSkin);
				}
			}
			return player.BeforeInvoke (info, out returnValue);
		}
		
		
		public override void AfterInvoke (InvocationInfo info, ref object returnValue)
		{
			player.AfterInvoke(info, ref returnValue);
		}

		public void ButtonClicked (UIListPopup popup, ECardListButton button)
		{
			return;
		}
		public void ButtonClicked (UIListPopup popup, ECardListButton button, List<Item> selectedCards)
		{
			return;
		}
		public void ItemButtonClicked (UIListPopup popup, Item card)
		{
			return;
		}
		public void ItemClicked (UIListPopup popup, Item card)
		{
			player.LaunchReplay (((Record)card).fileName());
		}
		public void ItemHovered (UIListPopup popup, Item card)
		{
			return;
		}
		
	}
	
	internal class Record : Item {
		private String Title;
		private String Description;
		private String filename;
		private ResourceType resource; 

		public Record(String title, String desc, String filename, ResourceType resource) {
			this.Title = title;
			this.Description = desc;
			this.filename = filename;
			this.resource = resource;
		}

		public bool selectable() {
			return true;
		}

		public Texture getImage() {
			switch (resource) {
			case ResourceType.GROWTH:
				return ResourceManager.LoadTexture ("BattleUI/battlegui_icon_growth");
			case ResourceType.ENERGY:
				return ResourceManager.LoadTexture ("BattleUI/battlegui_icon_energy");
			case ResourceType.ORDER:
				return ResourceManager.LoadTexture ("BattleUI/battlegui_icon_order");
			case ResourceType.DECAY:
				return ResourceManager.LoadTexture ("BattleUI/battlegui_icon_decay");
			default:
				return null;
			}
		}

		public String getName() {
			return Title;
		}

		public String getDesc() {
			return Description;
		}

		public String fileName() {
			return filename;
		}
	}
}

