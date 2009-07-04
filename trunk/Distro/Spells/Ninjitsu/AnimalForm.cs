using System;
using System.Collections;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Gumps;
using Server.Network;
using Server.Mobiles;
using Server.Targeting;
using Server.Spells.Fifth;
using Server.Spells.Seventh;

namespace Server.Spells.Ninjitsu
{
	public class AnimalForm : NinjaSpell
	{

		public static void Initialize()
		{
			EventSink.Login += new LoginEventHandler( OnLogin );
		}

		public static void OnLogin( LoginEventArgs e )
		{
			AnimalFormContext context = AnimalForm.GetContext( e.Mobile );

			if( context != null && context.SpeedBoost )
				e.Mobile.Send( SpeedControl.MountSpeed );
		}

		private static SpellInfo m_Info = new SpellInfo(
			"Animal Form", null,
			-1,
			9002
			);

		public override TimeSpan CastDelayBase { get { return TimeSpan.FromSeconds( 1.0 ); } }

		public override double RequiredSkill{ get{ return 0.0; } }
		public override int RequiredMana{ get{ return (Core.ML ? 10 : 0); } }
		public override int CastRecoveryBase{ get { return (Core.ML ? 10 : base.CastRecoveryBase); } }

		public override bool BlockedByAnimalForm{ get{ return false; } }

		public AnimalForm( Mobile caster, Item scroll ) : base( caster, scroll, m_Info )
		{
		}

		public override bool CheckCast()
		{
			if ( !Caster.CanBeginAction( typeof( PolymorphSpell ) ) )
			{
				Caster.SendLocalizedMessage( 1061628 ); // You can't do that while polymorphed.
				return false;
			}
			else if ( TransformationSpellHelper.UnderTransformation( Caster ) )
			{
				Caster.SendLocalizedMessage( 1063219 ); // You cannot mimic an animal while in that form.
				return false;
			}

			return base.CheckCast();
		}

		public override bool CheckDisturb( DisturbType type, bool firstCircle, bool resistable )
		{
			return false;
		}

		public override void OnBeginCast()
		{
			base.OnBeginCast();

			Caster.FixedEffect( 0x37C4, 10, 14, 4, 3 );
		}

		public override bool CheckFizzle()
		{
			// Spell is initially always successful, and with no skill gain.
			return true;
		}

		public override void OnCast()
		{
			if ( !Caster.CanBeginAction( typeof( PolymorphSpell ) ) )
			{
				Caster.SendLocalizedMessage( 1061628 ); // You can't do that while polymorphed.
			}
			else if( TransformationSpellHelper.UnderTransformation( Caster ) )
			{
				Caster.SendLocalizedMessage( 1063219 ); // You cannot mimic an animal while in that form.
			}
			else if ( !Caster.CanBeginAction( typeof( IncognitoSpell ) ) || (Caster.IsBodyMod && GetContext( Caster ) == null) )
			{
				DoFizzle();
			}
			else if ( CheckSequence() )
			{
				AnimalFormContext context = GetContext( Caster );

				if ( context != null )
				{
					RemoveContext( Caster, context, true );
				}
				else if ( Caster is PlayerMobile )
				{
					if ( GetLastAnimalForm( Caster ) == -1 || DateTime.Now - Caster.LastMoveTime > Caster.ComputeMovementSpeed( Caster.Direction ) )
					{
						Caster.CloseGump( typeof( AnimalFormGump ) );
						Caster.SendGump( new AnimalFormGump( Caster, m_Entries, this ) );
					}
					else
					{
						if ( Morph( Caster, GetLastAnimalForm( Caster ) ) == MorphResult.Fail )
							DoFizzle();
					}
				}
				else
				{
					if ( Morph( Caster, GetLastAnimalForm( Caster ) ) == MorphResult.Fail )
						DoFizzle();
				}
			}

			FinishSequence();
		}

		private static Hashtable m_LastAnimalForms = new Hashtable();

		public int GetLastAnimalForm( Mobile m )
		{
			if ( m_LastAnimalForms.Contains( m ) )
				return (int)m_LastAnimalForms[m];

			return -1;
		}

		public enum MorphResult
		{
			Success,
			Fail,
			NoSkill
		}

		public static MorphResult Morph( Mobile m, int entryID )
		{
			if ( entryID < 0 || entryID >= m_Entries.Length )
				return MorphResult.Fail;

			AnimalFormEntry entry = m_Entries[entryID];

			m_LastAnimalForms[m] = entryID;	//On OSI, it's the last /attempted/ one not the last succeeded one

			if ( m.Skills.Ninjitsu.Value < entry.ReqSkill )
			{
				string args = String.Format( "{0}\t{1}\t ", entry.ReqSkill.ToString( "F1" ), SkillName.Ninjitsu );
				m.SendLocalizedMessage( 1063013, args ); // You need at least ~1_SKILL_REQUIREMENT~ ~2_SKILL_NAME~ skill to use that ability.
				return MorphResult.NoSkill;
			}

			/*
			if( !m.CheckSkill( SkillName.Ninjitsu, entry.ReqSkill, entry.ReqSkill + 37.5 ) )
				return MorphResult.Fail;
			 * 
			 * On OSI,it seems you can only gain starting at '0' using Animal form.  
			*/
			
			double ninjitsu = m.Skills.Ninjitsu.Value;

			if ( ninjitsu < entry.ReqSkill + 37.5 )
			{
				double chance = (ninjitsu - entry.ReqSkill) / 37.5;

				if ( chance < Utility.RandomDouble() )
					return MorphResult.Fail;
			}

			m.CheckSkill( SkillName.Ninjitsu, 0.0, 37.5 );

			BaseMount.Dismount( m );

			m.BodyMod = entry.BodyMod;

			if ( entry.HueMod > 0 )
				m.HueMod = entry.HueMod;

			if ( entry.SpeedBoost )
				m.Send( SpeedControl.MountSpeed );

			SkillMod mod = null;

			if ( entry.StealthBonus )
			{
				mod = new DefaultSkillMod( SkillName.Stealth, true, 20.0 );
				mod.ObeyCap = true;
				m.AddSkillMod( mod );
			}

			#region Mondain's Legacy
			if ( entry.StealingBonus )
			{
				mod = new DefaultSkillMod( SkillName.Stealing, true, 10.0 );
				mod.ObeyCap = true;
				m.AddSkillMod( mod );
			}

			m.Target = null;
			#endregion

			Timer timer = new AnimalFormTimer( m, entry.BodyMod, entry.HueMod );
			timer.Start();

			AddContext( m, new AnimalFormContext( timer, mod, entry.SpeedBoost, entry.Type, entry.StealingBonus ) );
			return MorphResult.Success;
		}


		private static Hashtable m_Table = new Hashtable();

		public static void AddContext( Mobile m, AnimalFormContext context )
		{
			m_Table[m] = context;

			if ( context.Type == typeof( BakeKitsune ) || context.Type == typeof( GreyWolf ) )
				m.CheckStatTimers();
		}

		public static void RemoveContext( Mobile m, bool resetGraphics )
		{
			AnimalFormContext context = GetContext( m );

			if ( context != null )
				RemoveContext( m, context, resetGraphics );
		}

		public static void RemoveContext( Mobile m, AnimalFormContext context, bool resetGraphics )
		{
			m_Table.Remove( m );
			
			#region Mondain's Legacy
			if ( context.SpeedBoost )
			{
				if ( m.Region is Server.Regions.TwistedWealdDesert )
					m.Send( SpeedControl.WalkSpeed );
				else
					m.Send( SpeedControl.Disable );
			}
			#endregion

			SkillMod mod = context.Mod;

			if ( mod != null )
				m.RemoveSkillMod( mod );

			if ( resetGraphics )
			{
				m.HueMod = -1;
				m.BodyMod = 0;
			}

			context.Timer.Stop();
		}

		public static AnimalFormContext GetContext( Mobile m )
		{
			return ( m_Table[m] as AnimalFormContext );
		}

		public static bool UnderTransformation( Mobile m )
		{
			return ( GetContext( m ) != null );
		}

		public static bool UnderTransformation( Mobile m, Type type )
		{
			AnimalFormContext context = GetContext( m );

			return ( context != null && context.Type == type );
		}
/*
		private delegate void AnimalFormCallback( Mobile from );
		private delegate bool AnimalFormRequirementCallback( Mobile from );
*/
 		public class AnimalFormEntry
		{
			private Type m_Type;
			private TextDefinition m_Name;
			private int m_ItemID;
			private int m_Hue;
			private int m_Tooltip;
			private double m_ReqSkill;
			private int m_BodyMod;
			private int m_HueMod;
			private bool m_StealthBonus;
			private bool m_SpeedBoost;

			public Type Type{ get{ return m_Type; } }
			public TextDefinition Name{ get{ return m_Name; } }
			public int ItemID{ get{ return m_ItemID; } }
			public int Hue{ get{ return m_Hue; } }
			public int Tooltip{ get{ return m_Tooltip; } }
			public double ReqSkill{ get{ return m_ReqSkill; } }
			public int BodyMod{ get{ return m_BodyMod; } }
			public int HueMod{ get{ return m_HueMod; } }
			public bool StealthBonus{ get{ return m_StealthBonus; } }
			public bool SpeedBoost{ get{ return m_SpeedBoost; } }

			#region Mondain's Legacy
			private int m_Width;
			private int m_Height;
			private bool m_StealingBonus;
			private Type m_Talisman;

			public int Width{ get{ return m_Width; } }
			public int Height{ get{ return m_Height; } }
			public bool StealingBonus { get{ return m_StealingBonus; } }
			public Type Talisman{ get{ return m_Talisman; } }
			#endregion
			/*
			private AnimalFormCallback m_TransformCallback;
			private AnimalFormCallback m_UntransformCallback;
			private AnimalFormRequirementCallback m_RequirementCallback;
			*/
			public AnimalFormEntry( Type type, TextDefinition name, int itemID, int hue, int width, int height, int tooltip, double reqSkill, int bodyMod, bool stealthBonus, bool speedBoost, bool stealingBonus, Type talisman )
				: this( type, name, itemID, hue, width, height, tooltip, reqSkill, bodyMod, 0, stealthBonus, speedBoost, stealingBonus, talisman )
			{
			}

			public AnimalFormEntry( Type type, TextDefinition name, int itemID, int hue, int width, int height, int tooltip, double reqSkill, int bodyMod, int hueMod, bool stealthBonus, bool speedBoost, bool stealingBonus, Type talisman )
			{
				m_Type = type;
				m_Name = name;
				m_ItemID = itemID;
				m_Width = width; 
				m_Height = height;
				m_Hue = hue;
				m_Tooltip = tooltip;
				m_ReqSkill = reqSkill;
				m_BodyMod = bodyMod;
				m_HueMod = hueMod;
				m_StealthBonus = stealthBonus;
				m_SpeedBoost = speedBoost;
				m_StealingBonus = stealingBonus;
				m_Talisman = talisman;
			}
		}

		private static AnimalFormEntry[] m_Entries = new AnimalFormEntry[]
			{
				#region Mondain's Legacy
				new AnimalFormEntry( typeof( Kirin ),			1029632,	9632,    0,  6, 10, 1070811, 100.0, 0x84, false,  true, false, null ),
				new AnimalFormEntry( typeof( Unicorn ),			1018214,	9678,    0, 20, 10, 1070812, 100.0, 0x7A, false,  true, false, null ),
				new AnimalFormEntry( typeof( BakeKitsune ),		1030083,   10083,    0, 15, 15, 1070810,	 82.5, 0xF6, false,  true, false, null ),
				new AnimalFormEntry( typeof( GreyWolf ),		1028482,	9681, 2309, 25, 10, 1070810,  82.5, 0x19, false,  true, false, null ),
				new AnimalFormEntry( typeof( Llama ),			1028438,	8438,    0, 15,  8, 1070809,  70.0, 0xDC, false,  true, false, null ),
				new AnimalFormEntry( typeof( ForestOstard ),	1018273,	8503, 2212, 12, 10, 1070809,  70.0, 0xDA, false,  true, false, null ),
				new AnimalFormEntry( typeof( BullFrog ),		1028496,	8496, 2003, 15, 20, 1070807,  50.0, 0x51, 0x5A3, false, false, false, null ),
				new AnimalFormEntry( typeof( GiantSerpent ),	1018114,	9663, 2009,  8,  7, 1070808,  50.0, 0x15, false, false, false, null ),
				new AnimalFormEntry( typeof( Dog ),				1018280,	8476, 2309, 16, 17, 1070806,  40.0, 0xD9, false, false, false, null ),
				new AnimalFormEntry( typeof( Cat ),				1018264,	8475, 2309, 18, 17, 1070806,  40.0, 0xC9, false, false, false, null ),
				new AnimalFormEntry( typeof( Rat ),				1018294,	8483, 2309, 15, 20, 1070805,  20.0, 0xEE,  true, false, false, null ),
				new AnimalFormEntry( typeof( Rabbit ),			1028485,	8485, 2309, 19, 20, 1070805,  20.0, 0xCD,  true, false, false, null )
				#endregion
			};

		public static AnimalFormEntry[] Entries{ get{ return m_Entries; } }

		public class AnimalFormGump : Gump
		{
			#region Mondain's Legacy
			private const int EntriesPerPage = 10;
			private const int EntryHeight = 64;
			#endregion

			private Mobile m_Caster;
			private AnimalForm m_Spell;

			public AnimalFormGump( Mobile caster, AnimalFormEntry[] entries, AnimalForm spell ) : base( 50, 50 )
			{
				m_Caster = caster;
				m_Spell = spell;

				#region Mondain's Legacy
				AddPage( 0 );

				AddBackground( 0, 0, 520, 84 + EntriesPerPage * EntryHeight / 2, 0x13BE );
				AddImageTiled( 10, 10, 500, 20, 0xA40 );
				AddImageTiled( 10, 40, 500, 4 + EntriesPerPage * EntryHeight / 2, 0xA40 );
				AddImageTiled( 10, 54 + EntriesPerPage * EntryHeight / 2, 500, 20, 0xA40 );
				AddAlphaRegion( 10, 10, 500, 64 + EntriesPerPage * EntryHeight / 2 );
				AddButton( 10, 54 + EntriesPerPage * EntryHeight / 2, 0xFB1, 0xFB2, 0, GumpButtonType.Reply, 0 );
				AddHtmlLocalized( 45, 56 + EntriesPerPage * EntryHeight / 2, 450, 20, 1060051, 0x7FFF, false, false ); // CANCEL
				AddHtmlLocalized( 14, 12, 500, 20, 1063394, 0x7FFF, false, false ); // <center>Animal Form Selection Menu</center>

				double ninjitsu = caster.Skills.Ninjitsu.Value;

				Dictionary<int, AnimalFormEntry> list = new Dictionary<int, AnimalFormEntry>();

				for ( int i = 0; i < entries.Length; i++ )
				{
					bool enabled = ( ninjitsu >= entries[ i ].ReqSkill );

					if ( entries[ i ].Talisman != null && ( caster.Talisman == null || caster.Talisman.GetType() != entries[ i ].Talisman ) )
						enabled = false;

					if ( enabled )
						list.Add( i, entries[ i ] );
				}

				AnimalFormEntry entry;
				int pages = list.Count / EntriesPerPage + 1;
				int relative, page;
				int idx = 0, x = 0, y = 44;

				foreach ( KeyValuePair<int, AnimalFormEntry> kvp in list )
				{
					entry = kvp.Value;
					relative = idx % EntriesPerPage;
					x = idx % 2 == 0 ? 14 : 264;

					if ( relative == 0 )
					{
						page = idx / EntriesPerPage + 1;
						y = 44;
						AddPage( page );

						if ( idx > 0 )
						{
							AddButton( 300, 54 + EntriesPerPage * EntryHeight / 2, 0xFAE, 0xFB0, 0, GumpButtonType.Page, page - 1 );
							AddHtmlLocalized( 340, 56 + EntriesPerPage * EntryHeight / 2, 60, 20, 1011393, 0x7FFF, false, false ); // Back
						}

						if ( page < pages )
						{
							AddButton( 400, 54 + EntriesPerPage * EntryHeight / 2, 0xFA5, 0xFA7, 0, GumpButtonType.Page, page + 1 );
							AddHtmlLocalized( 440, 56 + EntriesPerPage * EntryHeight / 2, 60, 20, 1043353, 0x7FFF, false, false ); // Next
						}
					}

					AddImageTiledButton( x, y, 0x918, 0x919, 0x64 + kvp.Key, GumpButtonType.Reply, 0, entry.ItemID, 0x0, entry.Width, entry.Height );

					if ( entry.Tooltip > 0 )
						AddTooltip( entry.Tooltip );

					TextDefinition.AddHtmlText( this, x + 84, y, 250, 60, entry.Name, false, false, 0x7FFF, 0x7FFF );

					if ( idx % 2 == 1 )
						y += EntryHeight;

					idx++;
				}
				#endregion
			}

			public override void OnResponse( NetState sender, RelayInfo info )
			{
				#region Mondain's Legacy
				if ( info.ButtonID >= 100 )
				{
					int entryID = info.ButtonID - 100;
					
					if ( AnimalForm.Morph( m_Caster, entryID ) == MorphResult.Fail )
					{
						m_Caster.LocalOverheadMessage( MessageType.Regular, 0x3B2, 502632 ); // The spell fizzles.
						m_Caster.FixedParticles( 0x3735, 1, 30, 9503, EffectLayer.Waist );
						m_Caster.PlaySound( 0x5C );
					}
				}
				#endregion
			}
		}
	}

	public class AnimalFormContext
	{
		private Timer m_Timer;
		private SkillMod m_Mod;
		private bool m_SpeedBoost;
		private Type m_Type;

		public Timer Timer{ get{ return m_Timer; } }
		public SkillMod Mod{ get{ return m_Mod; } }
		public bool SpeedBoost{ get{ return m_SpeedBoost; } }
		public Type Type{ get{ return m_Type; } }

		#region Mondain's Legacy
		private bool m_StealingBonus;

		public bool StealingBonus{ get{ return m_StealingBonus; } }
		#endregion

		public AnimalFormContext( Timer timer, SkillMod mod, bool speedBoost, Type type, bool stealingBonus )
		{
			m_Timer = timer;
			m_Mod = mod;
			m_SpeedBoost = speedBoost;
			m_Type = type;

			#region Mondain's Legacy
			m_StealingBonus = stealingBonus;
			#endregion
		}
	}

	public class AnimalFormTimer : Timer
	{
		private Mobile m_Mobile;
		private int m_Body;
		private int m_Hue;

		public AnimalFormTimer( Mobile from, int body, int hue ) : base( TimeSpan.FromSeconds( 1.0 ), TimeSpan.FromSeconds( 1.0 ) )
		{
			m_Mobile = from;
			m_Body = body;
			m_Hue = hue;

			Priority = TimerPriority.FiftyMS;
		}

		protected override void OnTick()
		{
			if ( m_Mobile.Deleted || !m_Mobile.Alive || m_Mobile.Body != m_Body || (m_Hue != 0 && m_Mobile.Hue != m_Hue) )
			{
				AnimalForm.RemoveContext( m_Mobile, true );
				Stop();
			}
		}
	}
}