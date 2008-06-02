using System;
using Server.Items;

namespace Server.Items
{
	public class ScepterOfTheChief : Scepter
	{
		public override int LabelNumber{ get{ return 1072080; } } // Scepter of the Chief

		[Constructable]
		public ScepterOfTheChief() : base()
		{
			Hue = 0x481;
			
			Slayer = SlayerName.Exorcism;
			
			Attributes.RegenHits = 2;
			Attributes.ReflectPhysical = 15;
			Attributes.WeaponDamage = 45;
			
			WeaponAttributes.HitDispel = 100;
			WeaponAttributes.HitLeechMana = 100;
		}

		public ScepterOfTheChief( Serial serial ) : base( serial )
		{
		}
		
		public override void GetDamageTypes( Mobile wielder, out int phys, out int fire, out int cold, out int pois, out int nrgy )
		{
			phys = fire = cold = nrgy = 0;
			pois = 100;
		}

		public override void Serialize( GenericWriter writer )
		{
			base.Serialize( writer );

			writer.WriteEncodedInt( 0 ); // version
		}

		public override void Deserialize( GenericReader reader )
		{
			base.Deserialize( reader );

			int version = reader.ReadEncodedInt();
		}
	}
}