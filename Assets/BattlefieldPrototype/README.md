# Wardens And Raiders (WAR): hand-controlled tower commander

Summon allies with gold from the SUMMON page. Cards remain available after purchase:
repeat any card to buy another guard (10 gold), archer (12), or healer (14).
The five cards remain repeatable, with no cap on living allies. Summoning requires
enough gold and clear deployment space. Towers automatically generate gold.
Enemy purchases accompany player purchases when affordable, and the enemy also
recruits reinforcements independently during battle.

There is no PLAY AS or character possession in tower battles. Allies fight automatically;
PLAY provides ADVANCE, HOLD and GUARD orders. Capture the enemy tower to win.
Losing your current squad does not prevent summoning reinforcements.

All controls work by hand pointing and pinching. MENU opens controls; CLOSE MENU hides them.
BUILD opens a placement map with ROTATE and CANCEL buttons. The two-handed bow retains its
trajectory guide: left hand holds, right hand pinches the string, pulls and releases.

For Quest Link, stop Unity Play, allow compilation, then start Play again.
Latest focused verification: LocalValidation/RepeatSummons.xml. Earlier validation reports
refer to earlier gameplay rules and must not be treated as current summoning requirements.

Defense update: timber 8 gold, stone 18, spiked 12, caltrops 6, blast mine 14.
Choose BUILD > a defense > PLACE IN WORLD. The menu closes without canceling
placement. Point at clear ground inside a gold zone; a green footprint means
valid placement. Pinch once to buy one defense, then release before placing
another. MENU returns to the placement map for ROTATE, CHANGE TYPE or CANCEL.
Selecting PLAY exits building mode. World placement uses the stabilized hand
pointer so closing a pinch does not shift the selected location.
Validation: LocalValidation/WorldBuild.xml (four focused Unity tests passed,
including actual optical-input world placement of all five defense types).
Physical Quest Link tracking remains unverified by these synthetic tests.
