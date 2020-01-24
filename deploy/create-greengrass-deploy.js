var AWS = require("aws-sdk");
var greengrass = new AWS.Greengrass();

(async () => {
  try {
    // 当面複数グループを作らないので先頭を使う
    const groups = await greengrass.listGroups({}).promise();
    const groupId = groups.Groups[0].Id;
    const groupVersions = await greengrass
      .listGroupVersions({
        GroupId: groupId
      })
      .promise();
    var groupVersion = groupVersions.Versions[0];
    const deployment = await greengrass
      .createDeployment({
        GroupId: groupId,
        GroupVersionId: groupVersion.Version,
        DeploymentType: "NewDeployment"
      })
      .promise();
    console.log(deployment);
  } catch (err) {
    console.error(err);
  }
})();
