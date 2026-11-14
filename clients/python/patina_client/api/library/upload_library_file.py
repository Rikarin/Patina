from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.uploaded_file import UploadedFile
from ...types import File, FileTypes
from io import BytesIO
from typing import cast



def _get_kwargs(
    *,
    body: File,
    name: str,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    params: dict[str, Any] = {}

    params["name"] = name


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/library/uploads",
        "params": params,
    }

    _kwargs["content"] = body.payload
    headers["Content-Type"] = "application/octet-stream"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | UploadedFile | None:
    if response.status_code == 200:
        response_200 = UploadedFile.from_dict(response.json())



        return response_200

    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if response.status_code == 413:
        response_413 = ProblemDetails.from_dict(response.json())



        return response_413

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | UploadedFile]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: File,
    name: str,

) -> Response[ProblemDetails | UploadedFile]:
    """ Keeps a picture or a mesh a client sent from its own computer in the library's uploads folder, and
    answers with its path on the Server's machine, which a project's mesh, a picture brought into a
    project and a bake's high mesh take. The same file sent again is kept once.

    Args:
        name (str):
        body (File):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | UploadedFile]
     """


    kwargs = _get_kwargs(
        body=body,
name=name,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: File,
    name: str,

) -> ProblemDetails | UploadedFile | None:
    """ Keeps a picture or a mesh a client sent from its own computer in the library's uploads folder, and
    answers with its path on the Server's machine, which a project's mesh, a picture brought into a
    project and a bake's high mesh take. The same file sent again is kept once.

    Args:
        name (str):
        body (File):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | UploadedFile
     """


    return sync_detailed(
        client=client,
body=body,
name=name,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: File,
    name: str,

) -> Response[ProblemDetails | UploadedFile]:
    """ Keeps a picture or a mesh a client sent from its own computer in the library's uploads folder, and
    answers with its path on the Server's machine, which a project's mesh, a picture brought into a
    project and a bake's high mesh take. The same file sent again is kept once.

    Args:
        name (str):
        body (File):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | UploadedFile]
     """


    kwargs = _get_kwargs(
        body=body,
name=name,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: File,
    name: str,

) -> ProblemDetails | UploadedFile | None:
    """ Keeps a picture or a mesh a client sent from its own computer in the library's uploads folder, and
    answers with its path on the Server's machine, which a project's mesh, a picture brought into a
    project and a bake's high mesh take. The same file sent again is kept once.

    Args:
        name (str):
        body (File):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | UploadedFile
     """


    return (await asyncio_detailed(
        client=client,
body=body,
name=name,

    )).parsed
