using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FNPlugin {
	class FNRadiator : FNResourceSuppliableModule	{
		[KSPField(isPersistant = true)]
		public bool IsEnabled;
		[KSPField(isPersistant = false)]
		public bool isDeployable = true;
		[KSPField(isPersistant = false)]
		public float convectiveBonus = 1.0f;
		[KSPField(isPersistant = false)]
		public string animName;
		[KSPField(isPersistant = false)]
		public float radiatorTemp;
		[KSPField(isPersistant = false)]
		public float radiatorArea;
		[KSPField(isPersistant = false)]
		public string originalName;
		[KSPField(isPersistant = true)]
		public bool isupgraded = false;
		[KSPField(isPersistant = false)]
		public float upgradeCost = 100;
		[KSPField(isPersistant = false)]
		public string upgradedName;
		[KSPField(isPersistant = false)]
		public float upgradedRadiatorTemp;
		[KSPField(isPersistant = true)]
		public bool radiatorInit = false;
		[KSPField(isPersistant = false)]
		public string upgradeTechReq;

		[KSPField(isPersistant = false, guiActive = true, guiName = "Type")]
		public string radiatorType;
		[KSPField(isPersistant = false, guiActive = true, guiName = "Temperature")]
		public string radiatorTempStr;
		[KSPField(isPersistant = false, guiActive = true, guiName = "Power Radiated")]
		public string thermalPowerDissipStr;
		[KSPField(isPersistant = false, guiActive = true, guiName = "Power Convected")]
		public string thermalPowerConvStr;
		[KSPField(isPersistant = false, guiActive = true, guiName = "Upgrade")]
		public string upgradeCostStr;

		public const double stefan_const = 5.6704e-8;
		public const float h = 1000;

		protected const string emissive_property_name = "_Emission";

		protected Animation anim;
		protected float radiatedThermalPower;
		protected float convectedThermalPower;
		protected float current_rad_temp;
		protected float myScience = 0;
		protected Vector3 oldrot;
		protected float directionrotate = 1;
		protected float oldangle = 0;
		protected Vector3 original_eulers;
		protected Transform pivot;
		protected Texture orig_emissive_colour;
		protected double last_draw_update = 0.0;
		protected bool hasrequiredupgrade;
		protected int explode_counter = 0;

		protected static List<FNRadiator> list_of_radiators = new List<FNRadiator>();


		public static List<FNRadiator> getRadiatorsForVessel(Vessel vess) {
			List<FNRadiator> list_of_radiators_for_vessel = new List<FNRadiator>();
			list_of_radiators.RemoveAll(item => item == null);
			foreach (FNRadiator radiator in list_of_radiators) {
				if (radiator.vessel == vess) {
					list_of_radiators_for_vessel.Add (radiator);
				}
			}
			return list_of_radiators_for_vessel;
		}

		public static bool hasRadiatorsForVessel(Vessel vess) {
			list_of_radiators.RemoveAll(item => item == null);
			bool has_radiators = false;
			foreach (FNRadiator radiator in list_of_radiators) {
				if (radiator.vessel == vess) {
					has_radiators = true;
				}
			}
			return has_radiators;
		}

		public static float getAverageRadiatorTemperatureForVessel(Vessel vess) {
			list_of_radiators.RemoveAll(item => item == null);
			float average_temp = 0;
			float n_radiators = 0;
			foreach (FNRadiator radiator in list_of_radiators) {
				if (radiator.vessel == vess) {
					average_temp += radiator.getRadiatorTemperature ();
					n_radiators+=1.0f;
				}
			}

			if (n_radiators > 0) {
				average_temp = average_temp / n_radiators;
			} else {
				average_temp = 0;
			}

			return average_temp;
		}


		[KSPEvent(guiActive = true, guiName = "Deploy Radiator", active = true)]
		public void DeployRadiator() {
			if (!isDeployable) {
				return;
			}
			anim [animName].speed = 1f;
			anim [animName].normalizedTime = 0f;
			anim.Blend (animName, 2f);
			IsEnabled = true;
		}

		[KSPEvent(guiActive = true, guiName = "Retract Radiator", active = false)]
		public void RetractRadiator() {
			if (!isDeployable) {
				return;
			}
			anim [animName].speed = -1f;
			anim [animName].normalizedTime = 1f;
			anim.Blend (animName, 2f);
			IsEnabled = false;
		}

		[KSPEvent(guiActive = true, guiName = "Retrofit", active = true)]
		public void RetrofitRadiator() {
			if (ResearchAndDevelopment.Instance == null) { return;} 
			if (isupgraded || ResearchAndDevelopment.Instance.Science < upgradeCost) { return; }

			isupgraded = true;
			radiatorType = upgradedName;
			radiatorTemp = upgradedRadiatorTemp;
			radiatorTempStr = radiatorTemp + "K";

			ResearchAndDevelopment.Instance.Science = ResearchAndDevelopment.Instance.Science - upgradeCost;
		}

		[KSPAction("Deploy Radiator")]
		public void DeployRadiatorAction(KSPActionParam param) {
			DeployRadiator();
		}

		[KSPAction("Retract Radiator")]
		public void RetractRadiatorAction(KSPActionParam param) {
			RetractRadiator();
		}

		[KSPAction("Toggle Radiator")]
		public void ToggleRadiatorAction(KSPActionParam param) {
			if (IsEnabled) {
				RetractRadiator();
			} else {
				DeployRadiator();
			}
		}

		public override void OnStart(PartModule.StartState state) {
			Actions["DeployRadiatorAction"].guiName = Events["DeployRadiator"].guiName = String.Format("Deploy Radiator");
			Actions["RetractRadiatorAction"].guiName = Events["RetractRadiator"].guiName = String.Format("Retract Radiator");
			Actions["ToggleRadiatorAction"].guiName = String.Format("Toggle Radiator");

			if (state == StartState.Editor) { return; }
			this.part.force_activate();

			FNRadiator.list_of_radiators.Add (this);

			anim = part.FindModelAnimators (animName).FirstOrDefault ();
			//orig_emissive_colour = part.renderer.material.GetTexture (emissive_property_name);
			if (anim != null) {
				anim [animName].layer = 1;
				if (!IsEnabled) {
					anim [animName].normalizedTime = 1f;
					anim [animName].speed = -1f;

				} else {
					anim [animName].normalizedTime = 0f;
					anim [animName].speed = 1f;

				}
				anim.Play ();
			}

			if (isDeployable) {
				pivot = part.FindModelTransform ("suntransform");
				original_eulers = pivot.transform.localEulerAngles;
			} else {
				IsEnabled = true;
			}

			if(HighLogic.CurrentGame.Mode == Game.Modes.CAREER) {
				if(PluginHelper.hasTech(upgradeTechReq)) {
					hasrequiredupgrade = true;
				}
			}else{
				hasrequiredupgrade = true;
			}

			if (radiatorInit == false) {
				radiatorInit = true;
				if(hasrequiredupgrade) {
					isupgraded = true;
				}
			}

			if (!isupgraded) {
				radiatorType = originalName;
			} else {
				radiatorType = upgradedName;
				radiatorTemp = upgradedRadiatorTemp;
			}


			radiatorTempStr = radiatorTemp + "K";
		}

		public override void OnUpdate() {
			Events["DeployRadiator"].active = !IsEnabled && isDeployable;
			Events["RetractRadiator"].active = IsEnabled && isDeployable;
			if (ResearchAndDevelopment.Instance != null) {
				Events ["RetrofitRadiator"].active = !isupgraded && ResearchAndDevelopment.Instance.Science >= upgradeCost && hasrequiredupgrade;
			} else {
				Events ["RetrofitRadiator"].active = false;
			}
			Fields["upgradeCostStr"].guiActive = !isupgraded && hasrequiredupgrade;

			if (ResearchAndDevelopment.Instance != null) {
				upgradeCostStr = ResearchAndDevelopment.Instance.Science + "/" + upgradeCost.ToString ("0") + " Science";
			}

			if (Environment.TickCount - last_draw_update > 40) {
				thermalPowerDissipStr = radiatedThermalPower.ToString ("0.000") + "MW";
				thermalPowerConvStr = convectedThermalPower.ToString ("0.000") + "MW";
				radiatorTempStr = current_rad_temp.ToString ("0.0") + "K / " + radiatorTemp.ToString ("0.0") + "K";

				last_draw_update = Environment.TickCount;
			}
		}

		public override void OnFixedUpdate() {
			float atmosphere_height = vessel.mainBody.maxAtmosphereAltitude;
			float vessel_height = (float) vessel.mainBody.GetAltitude (vessel.transform.position);
			float conv_power_dissip = 0;
			if (vessel.altitude <= PluginHelper.getMaxAtmosphericAltitude(vessel.mainBody)) {
				float pressure = (float) FlightGlobals.getStaticPressure (vessel.transform.position);
				float dynamic_pressure = (float) (0.5*pressure*1.2041*vessel.srf_velocity.sqrMagnitude/101325.0);
				pressure += dynamic_pressure;
				float low_temp = FlightGlobals.getExternalTemperature (vessel.transform.position);

				float delta_temp = Mathf.Max(0,radiatorTemp - low_temp);
				conv_power_dissip = pressure * delta_temp * radiatorArea * h/1e6f * TimeWarp.fixedDeltaTime*convectiveBonus;
				if (!IsEnabled) {
					conv_power_dissip = conv_power_dissip / 2.0f;
				}
				convectedThermalPower = consumeFNResource (conv_power_dissip, FNResourceManager.FNRESOURCE_WASTEHEAT) / TimeWarp.fixedDeltaTime;

				if (IsEnabled && dynamic_pressure > 1.4854428818159388107574636072046e-3 && isDeployable) {
					part.deactivate();

					//part.breakingForce = 1;
					//part.breakingTorque = 1;
					part.decouple (1);
				}
			}


			if (IsEnabled) {
				if (getResourceBarRatio (FNResourceManager.FNRESOURCE_WASTEHEAT) >= 1 && current_rad_temp >= radiatorTemp) {
					explode_counter ++;
					if (explode_counter > 25) {
						part.explode ();
					}
				} else {
					explode_counter = 0;
				}

				double radiator_temperature_temp_val = radiatorTemp;
				if (FNReactor.hasActiveReactors (vessel)) {
					radiator_temperature_temp_val = Math.Min (FNReactor.getTemperatureofHottestReactor (vessel)/1.2, radiator_temperature_temp_val);
				}

				float thermal_power_dissip = (float)(stefan_const * radiatorArea * Math.Pow (radiator_temperature_temp_val, 4) / 1e6) * TimeWarp.fixedDeltaTime;
				radiatedThermalPower = consumeFNResource (thermal_power_dissip, FNResourceManager.FNRESOURCE_WASTEHEAT) / TimeWarp.fixedDeltaTime;

				current_rad_temp = (float) (Math.Min(Math.Pow (radiatedThermalPower*1e6 / (stefan_const * radiatorArea), 0.25),radiatorTemp));
				current_rad_temp = Mathf.Max(current_rad_temp,FlightGlobals.getExternalTemperature((float)vessel.altitude,vessel.mainBody)+273.16f);

				if (isDeployable) {
					Vector3 pivrot = pivot.rotation.eulerAngles;

					pivot.Rotate (Vector3.up * 5f * TimeWarp.fixedDeltaTime * directionrotate);

					Vector3 sunpos = FlightGlobals.Bodies [0].transform.position;
					Vector3 flatVectorToTarget = sunpos - transform.position;

					flatVectorToTarget = flatVectorToTarget.normalized;
					float dot = Mathf.Asin (Vector3.Dot (pivot.transform.right, flatVectorToTarget)) / Mathf.PI * 180.0f;

					float anglediff = -dot;
					oldangle = dot;
					//print (dot);
					directionrotate = anglediff / 5 / TimeWarp.fixedDeltaTime;
					directionrotate = Mathf.Min (3, directionrotate);
					directionrotate = Mathf.Max (-3, directionrotate);
			
					part.maximum_drag = 0.8f;
					part.minimum_drag = 0.8f;
				}

			} else {
				if (isDeployable) {
					pivot.transform.localEulerAngles = original_eulers;
				}

				double radiator_temperature_temp_val = radiatorTemp;
				if (FNReactor.hasActiveReactors (vessel)) {
					radiator_temperature_temp_val = Math.Min (FNReactor.getTemperatureofHottestReactor (vessel)/1.2, radiator_temperature_temp_val);
				}

				float thermal_power_dissip = (float)(stefan_const * radiatorArea * Math.Pow (radiator_temperature_temp_val, 4) / 1e7) * TimeWarp.fixedDeltaTime;
				radiatedThermalPower = consumeFNResource (thermal_power_dissip, FNResourceManager.FNRESOURCE_WASTEHEAT) / TimeWarp.fixedDeltaTime;

				current_rad_temp = (float) (Math.Min(Math.Pow (radiatedThermalPower*1e6 / (stefan_const * radiatorArea), 0.25),radiatorTemp));
				current_rad_temp = Mathf.Max(current_rad_temp,FlightGlobals.getExternalTemperature((float)vessel.altitude,vessel.mainBody)+273.16f);

				part.maximum_drag = 0.2f;
				part.minimum_drag = 0.2f;
			}



		}

		public float getRadiatorTemperature() {
			return current_rad_temp;
		}

		public override string GetInfo() {
			float thermal_power_dissip = (float)(stefan_const * radiatorArea * Math.Pow (radiatorTemp, 4) / 1e6);
			float thermal_power_dissip2 = (float)(stefan_const * radiatorArea * Math.Pow (upgradedRadiatorTemp, 4) / 1e6);
			return String.Format("Waste Heat Radiated\n Present: {0} MW\n After Upgrade: {1} MW\n Upgrade Cost: {2} Science", thermal_power_dissip,thermal_power_dissip2,upgradeCost);
		}

	}
}
