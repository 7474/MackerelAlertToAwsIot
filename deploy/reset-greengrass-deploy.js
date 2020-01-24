var AWS = require("aws-sdk");
var greengrass = new AWS.Greengrass();

(async () => {
  try {
    // 当面複数グループを作らないので先頭を使う。
    const groups = await greengrass.listGroups({}).promise();
    const groupId = groups.Groups[0].Id;
    const reset = await greengrass
      .resetDeployments({
        GroupId: groupId,
        Force: true
      })
      .promise();
    console.log(reset);
  } catch (err) {
    console.error(err);
  }
})();
