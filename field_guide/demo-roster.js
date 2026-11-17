// These catalog IDs also identify their models in the Unity yard.
export const contestAnimals = [
  { id: "JJ-001834", name: "노란 고양이", modelKey: "tabby" },
  { id: "JJ-004729", name: "남방큰돌고래", modelKey: "dolphin" },
  { id: "JJ-003483", name: "참새", modelKey: "sparrow" },
  { id: "JJ-000821", name: "웰시코기", modelKey: "corgi" },
];
export const contestInsects = [
  { id: "JJ-000532", name: "섬서구메뚜기", modelKey: "grasshopper" },
  { id: "JJ-001114", name: "칠성무당벌레", modelKey: "ladybird" },
  { id: "JJ-004705", name: "장수풍뎅이", modelKey: "rhino_beetle" },
];
export const dogCoats = [["dog_black","검은색 강아지"],["dog_brown","갈색 강아지"],["dog_white","흰색 강아지"]]
  .map(([modelKey,name]) => ({id:"JJ-000821/"+modelKey, name, modelKey}));
export const exampleIds = [
  "JJ-000837", "JJ-004873", "JJ-003382", "JJ-003554", "JJ-002293", "JJ-003570",
  "JJ-003658", "JJ-004398", "JJ-003478", "JJ-003671", "JJ-002187", "JJ-000261",
];

export const exampleModelKeys = {
  "JJ-002187": "swallow",
  "JJ-000261": "mallard",
  "JJ-000837": "roe_deer",
  "JJ-002293": "bulbul",
  "JJ-004873": "white_eye",
  "JJ-003658": "magpie",
  "JJ-003671": "cabbage_white",
  "JJ-003382": "swallowtail",
};
