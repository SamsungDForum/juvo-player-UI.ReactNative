'use strict';
function obj2strSafe(obj2dump,indent)
{
  let cache = [];

  const dump = JSON.stringify(
    obj2dump, 
    (obj, value) => 
    {
      if (typeof value === 'object' && value !== null) 
      {
        // Duplicate reference found, discard key
        if (cache.includes(value)) return;
  
        // Store value in our collection
        cache.push(value);
      }
      return value;
    },
    indent );

  cache = null;
  return dump;
}

export const Debug = 
{
  stringify: function(obj2str, indent=2)
  {
    return obj2strSafe(obj2str,indent);
  },

  logLineByLine: function(str, separator ='\n')
  {
    const lineArr = str.split(separator);
    for(line of lineArr)
      console.debug(line);
  }
};